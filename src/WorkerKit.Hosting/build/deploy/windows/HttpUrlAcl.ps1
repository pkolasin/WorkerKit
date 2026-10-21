# Shared HTTP.sys URL-reservation helpers for install-service.ps1 and uninstall-service.ps1.
# LocalSystem does not get a reservation. A virtual account is NT SERVICE\<service name>.

function ConvertTo-HealthListenPrefix {
    param([string] $Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $null
    }

    $trimmed = $Url.Trim()
    if (-not $trimmed.EndsWith("/")) {
        $trimmed += "/"
    }

    $uri = $null
    $absolute = [Uri]::TryCreate($trimmed, [UriKind]::Absolute, [ref] $uri)
    if (-not $absolute -or $uri.Scheme -ne "http") {
        throw "Health:ListenUrl '$Url' must be an absolute http:// prefix."
    }

    return $trimmed
}

function Test-LocalSystemAccount {
    param([string] $Account)

    if ([string]::IsNullOrWhiteSpace($Account)) {
        return $true
    }

    switch ($Account.Trim().ToUpperInvariant()) {
        "LOCALSYSTEM" { return $true }
        "LOCAL SYSTEM" { return $true }
        "NT AUTHORITY\SYSTEM" { return $true }
        "NT AUTHORITY\LOCALSYSTEM" { return $true }
        ".\LOCALSYSTEM" { return $true }
        default { return $false }
    }
}

function Resolve-UrlAclAccount {
    param(
        [string] $ServiceAccount,
        [string] $ServiceName
    )

    if (Test-LocalSystemAccount $ServiceAccount) {
        return $null
    }

    $trimmed = $ServiceAccount.Trim()
    if ($trimmed -match '^(?i)NT SERVICE\\?(.*)$') {
        if ([string]::IsNullOrWhiteSpace($ServiceName)) {
            throw "A virtual account requires the Windows service name."
        }

        return "NT SERVICE\$ServiceName"
    }

    return $trimmed
}

function Get-HealthListenUrlFromJson {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $json = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $json.Health) {
        return $null
    }

    return [string] $json.Health.ListenUrl
}

function Set-HealthListenUrlInJson {
    param(
        [string] $Path,
        [string] $ListenUrl
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        throw "Cannot set Health:ListenUrl. Missing $Path."
    }

    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "Cannot set Health:ListenUrl. $Path is empty."
    }

    $json = $raw | ConvertFrom-Json
    if ($null -eq $json) {
        throw "Cannot set Health:ListenUrl. $Path is empty."
    }

    $normalized = ConvertTo-HealthListenPrefix $ListenUrl
    if ($null -eq $normalized) {
        throw "Cannot set Health:ListenUrl. The URL is empty."
    }

    $healthProperty = $json.PSObject.Properties['Health']
    if ($null -eq $healthProperty -or $null -eq $json.Health) {
        $json | Add-Member -NotePropertyName Health -NotePropertyValue ([pscustomobject]@{ ListenUrl = $normalized }) -Force
    }
    elseif ($json.Health -isnot [System.Management.Automation.PSCustomObject]) {
        throw "Cannot set Health:ListenUrl. Health in $Path is not a JSON object."
    }
    elseif ($null -eq $json.Health.PSObject.Properties['ListenUrl']) {
        $json.Health | Add-Member -NotePropertyName ListenUrl -NotePropertyValue $normalized
    }
    else {
        $json.Health.ListenUrl = $normalized
    }

    # -InputObject keeps a one-element array (Serilog.WriteTo) as an array.
    # Windows PowerShell 5.1 defaults -Depth to 2, which would flatten Ares.Resilience and Serilog.WriteTo[].Args.
    # Its ConvertTo-Json indent is wide and uneven, so re-indent the text afterwards.
    $text = Format-JsonText -Text (ConvertTo-Json -InputObject $json -Depth 100)
    if (-not $text.EndsWith("`n")) {
        $text += "`n"
    }

    # UTF8Encoding($false) writes UTF-8 without a BOM on both Windows PowerShell 5.1 and pwsh 7.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $text, $utf8)
    return $normalized
}

# Re-indents JSON text with 2 spaces. Strings and their escapes are copied unchanged.
# Windows PowerShell 5.1 has no System.Text.Json, so this stays in the language.
function Format-JsonText {
    param([string] $Text)

    if ($null -eq $Text) {
        return ""
    }

    $builder = New-Object System.Text.StringBuilder
    $indent = 0
    $inString = $false
    $escape = $false
    $length = $Text.Length
    for ($index = 0; $index -lt $length; $index++) {
        $char = $Text[$index]
        if ($inString) {
            [void]$builder.Append($char)
            if ($escape) {
                $escape = $false
            }
            elseif ($char -eq '\') {
                $escape = $true
            }
            elseif ($char -eq '"') {
                $inString = $false
            }

            continue
        }

        if ($char -eq ' ' -or $char -eq "`t" -or $char -eq "`r" -or $char -eq "`n") {
            continue
        }

        if ($char -eq '"') {
            $inString = $true
            [void]$builder.Append($char)
            continue
        }

        if ($char -eq '{' -or $char -eq '[') {
            $closer = '}'
            if ($char -eq '[') { $closer = ']' }
            $next = $index + 1
            while ($next -lt $length) {
                $look = $Text[$next]
                if ($look -ne ' ' -and $look -ne "`t" -and $look -ne "`r" -and $look -ne "`n") {
                    break
                }

                $next++
            }

            if ($next -lt $length -and $Text[$next] -eq $closer) {
                [void]$builder.Append($char)
                [void]$builder.Append($closer)
                $index = $next
                continue
            }

            $indent++
            [void]$builder.Append($char)
            [void]$builder.Append("`n")
            if ($indent -gt 0) {
                [void]$builder.Append((' ' * ($indent * 2)))
            }

            continue
        }

        if ($char -eq '}' -or $char -eq ']') {
            $indent--
            if ($indent -lt 0) { $indent = 0 }
            [void]$builder.Append("`n")
            if ($indent -gt 0) {
                [void]$builder.Append((' ' * ($indent * 2)))
            }

            [void]$builder.Append($char)
            continue
        }

        if ($char -eq ',') {
            [void]$builder.Append($char)
            [void]$builder.Append("`n")
            if ($indent -gt 0) {
                [void]$builder.Append((' ' * ($indent * 2)))
            }

            continue
        }

        if ($char -eq ':') {
            [void]$builder.Append(': ')
            continue
        }

        [void]$builder.Append($char)
    }

    return $builder.ToString()
}

function Get-UrlAclUsersFromNetshOutput {
    param(
        [string] $Output,
        [string] $Prefix
    )

    $reserved = $false
    $users = New-Object System.Collections.Generic.List[string]
    $wanted = ""
    if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
        $wanted = $Prefix.Trim().TrimEnd("/").ToUpperInvariant()
    }

    foreach ($line in ($Output -split "\r?\n")) {
        if ($line -match 'Reserved URL\s*:\s*(.+)\s*$') {
            $found = $Matches[1].Trim().TrimEnd("/").ToUpperInvariant()
            $reserved = ($wanted -eq "" -or $found -eq $wanted)
            continue
        }

        if ($reserved -and $line -match '^\s*User:\s*(.+)\s*$') {
            $users.Add($Matches[1].Trim())
        }
    }

    return [pscustomobject]@{
        Reserved = $reserved
        Users    = @($users)
    }
}

function Get-UrlAclInstallAction {
    param(
        [bool] $Reserved,
        [string[]] $Users,
        [string] $Account
    )

    if (-not $Reserved) {
        return "Add"
    }

    $mine = $false
    $others = 0
    foreach ($user in @($Users)) {
        if ([string]::IsNullOrWhiteSpace($user)) {
            continue
        }

        if ($user.Trim().Equals($Account, [StringComparison]::OrdinalIgnoreCase)) {
            $mine = $true
        }
        else {
            $others++
        }
    }

    if ($mine -and $others -eq 0) {
        return "Skip"
    }

    return "Warn"
}

function Get-UrlAclUninstallAction {
    param(
        [bool] $Reserved,
        [string[]] $Users,
        [string] $Account
    )

    if (-not $Reserved) {
        return "Skip"
    }

    $mine = $false
    $others = 0
    $listed = 0
    foreach ($user in @($Users)) {
        if ([string]::IsNullOrWhiteSpace($user)) {
            continue
        }

        $listed++
        if ($user.Trim().Equals($Account, [StringComparison]::OrdinalIgnoreCase)) {
            $mine = $true
        }
        else {
            $others++
        }
    }

    if ($listed -eq 0) {
        return "Warn"
    }

    if ($mine -and $others -eq 0) {
        return "Delete"
    }

    return "Warn"
}

function Get-ServiceConfigFromScQc {
    param([string] $Output)

    $startName = $null
    $binary = $null
    foreach ($line in ($Output -split "\r?\n")) {
        if ($line -match 'SERVICE_START_NAME\s*:\s*(.*?)\s*$') {
            $startName = $Matches[1].Trim()
        }

        if ($line -match 'BINARY_PATH_NAME\s*:\s*(.*?)\s*$') {
            $binary = $Matches[1].Trim()
        }
    }

    $installDir = $null
    $instanceDir = $null
    if (-not [string]::IsNullOrWhiteSpace($binary)) {
        $exe = $null
        if ($binary -match '^"([^"]+)"') {
            $exe = $Matches[1]
        }
        elseif ($binary -match "^'([^']+)'") {
            $exe = $Matches[1]
        }
        elseif ($binary -match '^(\S+)') {
            $exe = $Matches[1]
        }

        if ($exe) {
            $installDir = Split-Path -Parent $exe
        }

        if ($binary -match '--instance-dir\s+"([^"]+)"') {
            $instanceDir = $Matches[1]
        }
        elseif ($binary -match "--instance-dir\s+'([^']+)'") {
            $instanceDir = $Matches[1]
        }
        elseif ($binary -match '--instance-dir\s+(\S+)') {
            $instanceDir = $Matches[1]
        }
    }

    return [pscustomobject]@{
        StartName   = $startName
        InstallDir  = $installDir
        InstanceDir = $instanceDir
    }
}

function Invoke-Netsh {
    param([string[]] $ArgumentList)

    if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
        $previous = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
    }

    try {
        $output = & netsh @ArgumentList 2>&1 | Out-String
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output   = $output
        }
    }
    finally {
        if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
            $PSNativeCommandUseErrorActionPreference = $previous
        }
    }
}

function Add-HttpUrlReservation {
    param(
        [string] $Prefix,
        [string] $Account
    )

    $shown = Invoke-Netsh -ArgumentList @("http", "show", "urlacl", "url=$Prefix")
    $parsed = Get-UrlAclUsersFromNetshOutput -Output $shown.Output -Prefix $Prefix
    $action = Get-UrlAclInstallAction -Reserved $parsed.Reserved -Users $parsed.Users -Account $Account
    switch ($action) {
        "Skip" {
            Write-Host "URL reservation already present for $Account at $Prefix."
        }
        "Warn" {
            $owners = @($parsed.Users | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ", "
            if ([string]::IsNullOrWhiteSpace($owners)) {
                $owners = "an account this script could not read"
            }

            Write-Warning "URL reservation for $Prefix is held by $owners, not only $Account. Left it unchanged. To give it to this account, delete that reservation and run: netsh http add urlacl url=$Prefix user=`"$Account`""
        }
        "Add" {
            $added = Invoke-Netsh -ArgumentList @("http", "add", "urlacl", "url=$Prefix", "user=$Account")
            if ($added.ExitCode -ne 0) {
                throw "netsh http add urlacl failed for $Prefix ($($added.ExitCode)). $($added.Output)"
            }

            Write-Host "Reserved $Prefix for $Account."
        }
    }
}

function Remove-HttpUrlReservation {
    param(
        [string] $Prefix,
        [string] $Account
    )

    $shown = Invoke-Netsh -ArgumentList @("http", "show", "urlacl", "url=$Prefix")
    $parsed = Get-UrlAclUsersFromNetshOutput -Output $shown.Output -Prefix $Prefix
    $action = Get-UrlAclUninstallAction -Reserved $parsed.Reserved -Users $parsed.Users -Account $Account
    switch ($action) {
        "Skip" {
            Write-Host "No URL reservation to remove for $Account at $Prefix."
        }
        "Warn" {
            $owners = @($parsed.Users | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ", "
            if ([string]::IsNullOrWhiteSpace($owners)) {
                $owners = "an account this script could not read"
            }

            Write-Warning "Left the URL reservation for $Prefix in place. It is held by $owners, not only $Account."
        }
        "Delete" {
            $deleted = Invoke-Netsh -ArgumentList @("http", "delete", "urlacl", "url=$Prefix")
            if ($deleted.ExitCode -ne 0) {
                throw "netsh http delete urlacl failed for $Prefix ($($deleted.ExitCode)). $($deleted.Output)"
            }

            Write-Host "Removed URL reservation $Prefix for $Account."
        }
    }
}
