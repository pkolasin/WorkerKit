# Installs the published host as a Windows Service.
# Service name, display name, executable, install path, and instance root come from
# workerkit.app.json next to the published app. Windows PowerShell 5.1 compatible.
#
# Already published into the install directory (default C:\Program Files\<Name>):
#   cd <install>
#   .\deploy\windows\install-service.ps1 -HealthListenUrl "http://127.0.0.1:5088/health/"
#
# Or publish elsewhere and copy into the install directory (appsettings*.json already there are kept):
#   .\deploy\windows\install-service.ps1 -PublishDir C:\publish\MyConnector -HealthListenUrl "http://127.0.0.1:5088/health/"
#
# One named instance of a shared install (not started; instance data is kept on uninstall):
#   .\deploy\windows\install-service.ps1 -Instance ares -HealthListenUrl "http://127.0.0.1:5089/health/"
# Virtual account (no password): -ServiceAccount "NT SERVICE" reserves HTTP.sys for NT SERVICE\<service>-ares.
# -HealthListenUrl is written into Health:ListenUrl of the effective appsettings.json
# (the instance directory for a named instance, the install directory for the default instance)
# for every account, including LocalSystem. A non-LocalSystem account also gets the HTTP.sys reservation.
param(
    [string] $Name,
    [string] $Instance,
    [string] $InstallPath,
    [string] $InstancesRoot,
    [string] $PublishDir,
    [string] $DisplayName,
    [string] $ServiceAccount,
    [string] $ServicePassword,
    [string] $HealthListenUrl
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "HttpUrlAcl.ps1")
. (Join-Path $PSScriptRoot "WorkerKitApp.ps1")

$app = Get-WorkerKitApp
if (-not $InstallPath) { $InstallPath = [string] $app.windowsInstallPath }
if (-not $InstancesRoot) { $InstancesRoot = [Environment]::ExpandEnvironmentVariables([string] $app.windowsInstancesRoot) }

if ($ServicePassword -and -not $ServiceAccount) {
    throw "-ServicePassword requires -ServiceAccount."
}

if ($Instance) {
    if ($Instance -eq "default") {
        throw "Instance 'default' is the install directory. Omit -Instance to install that single service as $($app.serviceName)."
    }

    if ($Instance -notmatch '^[a-z0-9][a-z0-9-]{0,39}$') {
        throw "Instance name '$Instance' is invalid. Use 1-40 characters: a lowercase letter or digit, then lowercase letters, digits, or hyphens."
    }

    if (-not $Name) { $Name = "$($app.serviceName)-$Instance" }
    if (-not $DisplayName) { $DisplayName = "$($app.displayName) ($Instance)" }
}
else {
    if (-not $Name) { $Name = [string] $app.serviceName }
    if (-not $DisplayName) { $DisplayName = [string] $app.displayName }
}

$exe = Join-Path $InstallPath ([string] $app.windowsExecutable)
if ($PublishDir) {
    if (-not (Test-Path -LiteralPath $PublishDir)) {
        throw "Publish directory not found: $PublishDir"
    }

    $publishFull = [System.IO.Path]::GetFullPath($PublishDir)
    $installFull = [System.IO.Path]::GetFullPath($InstallPath)
    if (-not $publishFull.Equals($installFull, [StringComparison]::OrdinalIgnoreCase)) {
        $publishExe = Join-Path $PublishDir ([string] $app.windowsExecutable)
        if (-not (Test-Path -LiteralPath $publishExe)) {
            throw "Executable not found: $publishExe. Publish the host into $PublishDir first."
        }

        New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
        $runningHere = @(Get-CimInstance Win32_Service | Where-Object { $_.State -eq "Running" -and $_.PathName -like "*$exe*" })
        if ($runningHere.Count -gt 0) {
            throw ("Cannot copy into $InstallPath while these services are running: " + ($runningHere.Name -join ", ") + ". Stop them first.")
        }

        $hadNative = Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue
        if ($hadNative) {
            $previousNative = $Global:PSNativeCommandUseErrorActionPreference
            $Global:PSNativeCommandUseErrorActionPreference = $false
        }

        try {
            $exclude = @("/XF", ".lock", "instance.pid")
            $existingSettings = @(Get-ChildItem -LiteralPath $InstallPath -Filter "appsettings*.json" -File -ErrorAction SilentlyContinue)
            foreach ($settingsFile in $existingSettings) { $exclude += $settingsFile.Name }
            robocopy $PublishDir $InstallPath /E @exclude /NFL /NDL /NJH /NJS | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
        }
        finally {
            if ($hadNative) {
                $Global:PSNativeCommandUseErrorActionPreference = $previousNative
            }
        }
    }
}

if (-not (Test-Path $exe)) {
    throw "Executable not found: $exe. Publish the host into $InstallPath, or pass -PublishDir."
}

$instanceDir = $null
if ($Instance) {
    $instanceDir = Join-Path $InstancesRoot $Instance
    New-Item -ItemType Directory -Force -Path $instanceDir, "$instanceDir\logs", "$instanceDir\data", "$instanceDir\temp" | Out-Null
    if (-not (Test-Path "$instanceDir\appsettings.json")) {
        $template = Join-Path $InstallPath "appsettings.instance.template.json"
        if (-not (Test-Path $template)) {
            throw "Missing template: $template. Publish the host so appsettings.instance.template.json is next to the executable."
        }

        Copy-Item $template "$instanceDir\appsettings.json"
    }

    # Quoted because a Windows Service binary path with spaces must be quoted.
    $binary = "`"$exe`" run --instance-dir `"$instanceDir`" --instance $Instance"
}
else {
    $binary = "`"$exe`" run"
}

$settingsPath = if ($instanceDir) { Join-Path $instanceDir "appsettings.json" } else { Join-Path $InstallPath "appsettings.json" }
if (-not [string]::IsNullOrWhiteSpace($HealthListenUrl)) {
    $HealthListenUrl = Set-HealthListenUrlInJson -Path $settingsPath -ListenUrl $HealthListenUrl
    Write-Host "Set Health:ListenUrl to $HealthListenUrl in $settingsPath."
}

$existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $Name -Force
    }

    sc.exe delete $Name | Out-Host
    Start-Sleep -Seconds 2
}

New-Service -Name $Name -BinaryPathName $binary -DisplayName $DisplayName -StartupType Automatic
sc.exe description $Name "Generic worker host ($($app.name)$(if ($Instance) { ", instance $Instance" }))." | Out-Host

$urlAclAccount = Resolve-UrlAclAccount -ServiceAccount $ServiceAccount -ServiceName $Name
if ($ServiceAccount) {
    # One shared account for every instance, unless this invocation passes a different account.
    # Omit -ServiceAccount to keep LocalSystem. LocalSystem gets no URL reservation.
    if ($urlAclAccount -like "NT SERVICE\*") {
        if ($ServicePassword) {
            throw "-ServicePassword cannot be set for virtual account $urlAclAccount."
        }

        sc.exe config $Name obj= $urlAclAccount | Out-Host
        $ServiceAccount = $urlAclAccount
    }
    else {
        sc.exe config $Name obj= $ServiceAccount password= $ServicePassword | Out-Host
    }

    $aclTarget = if ($instanceDir) { $instanceDir } else { $InstallPath }
    icacls $aclTarget /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
}

if ($urlAclAccount) {
    if (-not $HealthListenUrl) {
        $HealthListenUrl = Get-HealthListenUrlFromJson -Path $settingsPath
    }

    $prefix = ConvertTo-HealthListenPrefix $HealthListenUrl
    if ($prefix) {
        Add-HttpUrlReservation -Prefix $prefix -Account $urlAclAccount
    }
    else {
        Write-Host "No Health:ListenUrl configured. Skipped the HTTP.sys URL reservation for $urlAclAccount."
    }
}

sc.exe failure $Name reset= 86400 actions= restart/60000/restart/60000// | Out-Host
if ($Instance) {
    Write-Host "Service $Name installed. Instance directory: $instanceDir"
    Write-Host "Start it with: Start-Service -Name $Name"
}
else {
    Start-Service -Name $Name
    Write-Host "Service $Name started."
}
