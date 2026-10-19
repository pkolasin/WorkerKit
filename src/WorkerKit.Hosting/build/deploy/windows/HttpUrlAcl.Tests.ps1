# Exercises the URL-reservation decisions without netsh or the Service Control Manager.
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "HttpUrlAcl.ps1")
. (Join-Path $PSScriptRoot "WorkerKitApp.ps1")

$failures = New-Object System.Collections.Generic.List[string]

function Assert-Equal($name, $actual, $expected) {
    if ($actual -ne $expected) {
        $script:failures.Add("${name}: expected '$expected' but was '$actual'")
    }
}

Assert-Equal "blank prefix" (ConvertTo-HealthListenPrefix "  ") $null
Assert-Equal "adds slash" (ConvertTo-HealthListenPrefix "http://127.0.0.1:5088/health") "http://127.0.0.1:5088/health/"
Assert-Equal "keeps slash" (ConvertTo-HealthListenPrefix " http://127.0.0.1:5088/ ") "http://127.0.0.1:5088/"

$threw = $false
try { ConvertTo-HealthListenPrefix "https://127.0.0.1:5088/health/" | Out-Null } catch { $threw = $true }
Assert-Equal "rejects https" $threw $true

Assert-Equal "omitted account" (Resolve-UrlAclAccount "" "Acme") $null
Assert-Equal "LocalSystem" (Resolve-UrlAclAccount "LocalSystem" "Acme") $null
Assert-Equal "SYSTEM" (Resolve-UrlAclAccount "NT AUTHORITY\SYSTEM" "Acme") $null
Assert-Equal "domain account" (Resolve-UrlAclAccount " DOMAIN\svc " "Acme-ares") "DOMAIN\svc"
Assert-Equal "virtual rewrite" (Resolve-UrlAclAccount "NT SERVICE\other" "Acme-ares") "NT SERVICE\Acme-ares"
Assert-Equal "virtual bare" (Resolve-UrlAclAccount "NT SERVICE" "Acme") "NT SERVICE\Acme"

Assert-Equal "install add" (Get-UrlAclInstallAction $false @() "DOMAIN\svc") "Add"
Assert-Equal "install skip" (Get-UrlAclInstallAction $true @("DOMAIN\svc") "domain\svc") "Skip"
Assert-Equal "install other user" (Get-UrlAclInstallAction $true @("NT SERVICE\Other") "DOMAIN\svc") "Warn"
Assert-Equal "install shared" (Get-UrlAclInstallAction $true @("DOMAIN\svc", "NT SERVICE\Other") "DOMAIN\svc") "Warn"
Assert-Equal "install unreadable" (Get-UrlAclInstallAction $true @() "DOMAIN\svc") "Warn"

Assert-Equal "uninstall missing" (Get-UrlAclUninstallAction $false @() "DOMAIN\svc") "Skip"
Assert-Equal "uninstall delete" (Get-UrlAclUninstallAction $true @("NT SERVICE\Acme-ares") "nt service\Acme-ares") "Delete"
Assert-Equal "uninstall other" (Get-UrlAclUninstallAction $true @("DOMAIN\svc") "NT SERVICE\Acme-ares") "Warn"
Assert-Equal "uninstall shared" (Get-UrlAclUninstallAction $true @("DOMAIN\svc", "NT SERVICE\Acme-ares") "NT SERVICE\Acme-ares") "Warn"

$sample = @"
URL Reservations:
-----------------

    Reserved URL            : http://127.0.0.1:5088/health/
        User: NT SERVICE\Acme-ares
            Listen: Yes
            Delegate: No

"@
$parsed = Get-UrlAclUsersFromNetshOutput -Output $sample -Prefix "http://127.0.0.1:5088/health/"
Assert-Equal "show reserved" $parsed.Reserved $true
Assert-Equal "show user" @($parsed.Users)[0] "NT SERVICE\Acme-ares"
$otherPrefix = Get-UrlAclUsersFromNetshOutput -Output $sample -Prefix "http://127.0.0.1:9/"
Assert-Equal "show other prefix" $otherPrefix.Reserved $false

$missing = Get-UrlAclUsersFromNetshOutput -Output "The system cannot find the file specified." -Prefix "http://127.0.0.1:5088/"
Assert-Equal "show missing" $missing.Reserved $false

$qc = @"
SERVICE_NAME: Acme-ares
        BINARY_PATH_NAME   : "C:\Program Files\Acme\Acme.Host.exe" run --instance-dir "D:\Acme\instances\ares" --instance ares
        SERVICE_START_NAME : NT SERVICE\Acme-ares
"@
$config = Get-ServiceConfigFromScQc $qc
Assert-Equal "qc account" $config.StartName "NT SERVICE\Acme-ares"
Assert-Equal "qc install" ($config.InstallDir -replace '\\','/') "C:/Program Files/Acme"
Assert-Equal "qc instance" ($config.InstanceDir -replace '\\','/') "D:/Acme/instances/ares"

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("ws-urlacl-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $settings = Join-Path $root "appsettings.json"
    [System.IO.File]::WriteAllText($settings, '{"Health":{"ListenUrl":"http://127.0.0.1:5088/health"}}', $utf8)
    Assert-Equal "json url" (Get-HealthListenUrlFromJson $settings) "http://127.0.0.1:5088/health"
    Assert-Equal "json missing" (Get-HealthListenUrlFromJson (Join-Path $root "missing.json")) $null

    $ico = "I$([char]0x010C)O"
    $fixture = @"
{
  "Worker": { "ExampleIco": "$ico" },
  "Ares": { "Resilience": { "MaxRetryAttempts": 3 } },
  "Health": { "PublisherPeriodSeconds": 60, "ListenUrl": "" },
  "Serilog": {
    "MinimumLevel": { "Override": { "Microsoft": "Warning" } },
    "WriteTo": [ { "Name": "Console", "Args": { "outputTemplate": "keep-me" } } ]
  }
}
"@
    [System.IO.File]::WriteAllText($settings, $fixture, $utf8)
    $written = Set-HealthListenUrlInJson -Path $settings -ListenUrl "http://127.0.0.1:5093/health"
    Assert-Equal "set returns prefix" $written "http://127.0.0.1:5093/health/"
    $round = Get-Content -LiteralPath $settings -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal "set listen" $round.Health.ListenUrl "http://127.0.0.1:5093/health/"
    Assert-Equal "set ico" $round.Worker.ExampleIco $ico
    Assert-Equal "set retry" ([int] $round.Ares.Resilience.MaxRetryAttempts) 3
    Assert-Equal "set period" ([int] $round.Health.PublisherPeriodSeconds) 60
    Assert-Equal "set override" $round.Serilog.MinimumLevel.Override.Microsoft "Warning"
    Assert-Equal "set template" $round.Serilog.WriteTo[0].Args.outputTemplate "keep-me"
    $raw = [System.IO.File]::ReadAllText($settings)
    if ($raw -notmatch '"WriteTo"\s*:\s*\[') {
        $script:failures.Add("set writeTo: expected a JSON array")
    }
    $bytes = [System.IO.File]::ReadAllBytes($settings)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    Assert-Equal "set no bom" $hasBom $false

    $noHealth = Join-Path $root "no-health.json"
    [System.IO.File]::WriteAllText($noHealth, '{"Worker":{"ExampleIco":"kept"}}', $utf8)
    Set-HealthListenUrlInJson -Path $noHealth -ListenUrl "http://127.0.0.1:5093/health/" | Out-Null
    $created = Get-Content -LiteralPath $noHealth -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal "create health" $created.Health.ListenUrl "http://127.0.0.1:5093/health/"
    Assert-Equal "create keeps worker" $created.Worker.ExampleIco "kept"

    $noListen = Join-Path $root "no-listen.json"
    [System.IO.File]::WriteAllText($noListen, '{"Health":{"PublisherPeriodSeconds":60}}', $utf8)
    Set-HealthListenUrlInJson -Path $noListen -ListenUrl "http://127.0.0.1:5094/" | Out-Null
    $added = Get-Content -LiteralPath $noListen -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal "add listen" $added.Health.ListenUrl "http://127.0.0.1:5094/"
    Assert-Equal "add keeps period" ([int] $added.Health.PublisherPeriodSeconds) 60

    $threwSet = $false
    try { Set-HealthListenUrlInJson -Path (Join-Path $root "missing.json") -ListenUrl "http://127.0.0.1:5093/health/" | Out-Null } catch { $threwSet = $true }
    Assert-Equal "set missing file" $threwSet $true

    $pretty = [System.IO.File]::ReadAllText($settings)
    $second = Set-HealthListenUrlInJson -Path $settings -ListenUrl "http://127.0.0.1:5093/health"
    Assert-Equal "format second returns" $second "http://127.0.0.1:5093/health/"
    $again = [System.IO.File]::ReadAllText($settings)
    Assert-Equal "format stable" $pretty $again
    if ($pretty -notmatch '(?m)^  "Health": \{$') {
        $script:failures.Add("format indent: expected a 2-space Health object")
    }
    if ($pretty -notmatch '(?m)^    "ListenUrl": ') {
        $script:failures.Add("format indent: expected a 4-space ListenUrl")
    }
    $parsedAgain = $again | ConvertFrom-Json
    Assert-Equal "format roundtrip listen" $parsedAgain.Health.ListenUrl "http://127.0.0.1:5093/health/"
    Assert-Equal "format roundtrip ico" $parsedAgain.Worker.ExampleIco $ico

    $sampleJson = '{"q":"a\"b\\c","n":[1,2],"empty":{},"nested":{"x":true}}'
    $formatted = Format-JsonText $sampleJson
    $reformatted = Format-JsonText $formatted
    Assert-Equal "format idempotent" $formatted $reformatted
    $sampleParsed = $formatted | ConvertFrom-Json
    Assert-Equal "format escape" $sampleParsed.q ("a" + [char]34 + "b\c")
    Assert-Equal "format number" ([int] $sampleParsed.n[1]) 2
    Assert-Equal "format bool" ([bool] $sampleParsed.nested.x) $true

    $self = Get-Process -Id $PID
    $selfPath = $null
    $selfPathProperty = $self.PSObject.Properties['Path']
    if ($null -ne $selfPathProperty -and -not [string]::IsNullOrWhiteSpace([string] $selfPathProperty.Value)) {
        $selfPath = [string] $selfPathProperty.Value
    }
    if ([string]::IsNullOrWhiteSpace($selfPath)) {
        $selfPath = $self.MainModule.FileName
    }
    $pidFile = Join-Path $root "instance.pid"
    [System.IO.File]::WriteAllText($pidFile, "$PID")
    Assert-Equal "live pid" (Test-WorkerKitLiveInstancePid -PidFile $pidFile -ExePath $selfPath) $true
    Assert-Equal "wrong exe" (Test-WorkerKitLiveInstancePid -PidFile $pidFile -ExePath (Join-Path $root "missing.exe")) $false
    [System.IO.File]::WriteAllText($pidFile, "not-a-pid")
    Assert-Equal "bad pid" (Test-WorkerKitLiveInstancePid -PidFile $pidFile -ExePath $selfPath) $false
    [System.IO.File]::WriteAllText($pidFile, "99999999")
    Assert-Equal "dead pid" (Test-WorkerKitLiveInstancePid -PidFile $pidFile -ExePath $selfPath) $false
    Assert-Equal "missing pid" (Test-WorkerKitLiveInstancePid -PidFile (Join-Path $root "no-pid") -ExePath $selfPath) $false

    $emptyRefusal = Format-WorkerKitDefaultInstanceRefusal -InstallPath "C:\Program Files\Acme" -ServiceNames @() -Traces @()
    if ($emptyRefusal -notlike "*services without --instance-dir: none; traces: none.*") {
        $script:failures.Add("empty refusal: $emptyRefusal")
    }
    $namedRefusal = Format-WorkerKitDefaultInstanceRefusal -InstallPath "C:\Program Files\Acme" -ServiceNames @("Acme") -Traces @("logs")
    if ($namedRefusal -notlike "*services without --instance-dir: Acme; traces: logs.*") {
        $script:failures.Add("named refusal: $namedRefusal")
    }

    $bash = (Get-Command bash -ErrorAction SilentlyContinue).Source
    if ($bash) {
        # Quotes survive Start-Process joining arguments on spaces. The trailing builtin keeps bash
        # from exec'ing sleep, so this process's command line still contains --instance-dir.
        $named = Start-Process -FilePath $bash -ArgumentList @("-c", '"sleep 30; true"', "--", "--instance-dir", "/tmp/ares") -PassThru
        Start-Sleep -Milliseconds 300
        try {
            if ($named.HasExited) {
                $script:failures.Add("named instance pid: bash exited $($named.ExitCode)")
            }
            else {
                $namedPath = $null
                $namedPathProperty = $named.PSObject.Properties["Path"]
                if ($null -ne $namedPathProperty -and -not [string]::IsNullOrWhiteSpace([string] $namedPathProperty.Value)) {
                    $namedPath = [string] $namedPathProperty.Value
                }
                if ([string]::IsNullOrWhiteSpace($namedPath)) {
                    $namedPath = $named.MainModule.FileName
                }
                [System.IO.File]::WriteAllText($pidFile, "$($named.Id)")
                $namedOutput = @(Test-WorkerKitLiveInstancePid -PidFile $pidFile -ExePath $namedPath 3>&1)
                $namedLive = @($namedOutput | Where-Object { $_ -is [bool] }) | Select-Object -Last 1
                Assert-Equal "named instance pid" $namedLive $false
                $namedWarningText = @($namedOutput | Where-Object { $_ -isnot [bool] } | ForEach-Object { $_.ToString() }) -join " "
                if ($namedWarningText -notlike "*--instance-dir*") {
                    $script:failures.Add("named instance pid: warning was '$namedWarningText'")
                }
            }
        }
        finally {
            if (-not $named.HasExited) { Stop-Process -Id $named.Id -Force -ErrorAction SilentlyContinue }
        }
    }
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ }
    exit 1
}

Write-Host "HttpUrlAcl checks passed."
