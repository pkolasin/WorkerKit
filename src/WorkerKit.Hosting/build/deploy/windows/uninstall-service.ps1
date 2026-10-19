# Removes the Windows Service entry. Instance files (configuration, logs, data) are left in place.
# Also deletes the HTTP.sys URL reservation when it belongs only to this service account.
# Install path and instance root come from workerkit.app.json, the same defaults as install-service.ps1.
# The reservation URL comes from Health:ListenUrl in the instance appsettings.json, or in the
# install-directory appsettings.json for the default instance. A directory parsed from the
# service binary path is preferred over that default.
# -HealthListenUrl overrides Health:ListenUrl. -ServiceAccount overrides the account read from the service.
param(
    [string] $Name,
    [string] $Instance,
    [string] $InstallPath,
    [string] $InstancesRoot,
    [string] $ServiceAccount,
    [string] $HealthListenUrl
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "HttpUrlAcl.ps1")
. (Join-Path $PSScriptRoot "WorkerKitApp.ps1")

$app = Get-WorkerKitApp
if (-not $InstallPath) { $InstallPath = [string] $app.windowsInstallPath }
if (-not $InstancesRoot) { $InstancesRoot = [Environment]::ExpandEnvironmentVariables([string] $app.windowsInstancesRoot) }

if ($Instance) {
    if ($Instance -eq "default") {
        throw "Instance 'default' is uninstalled by omitting -Instance (service name $($app.serviceName))."
    }

    if ($Instance -notmatch '^[a-z0-9][a-z0-9-]{0,39}$') {
        throw "Instance name '$Instance' is invalid. Use 1-40 characters: a lowercase letter or digit, then lowercase letters, digits, or hyphens."
    }

    if (-not $Name) { $Name = "$($app.serviceName)-$Instance" }
}
elseif (-not $Name) {
    $Name = [string] $app.serviceName
}

$existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
$serviceConfig = $null
if ($null -ne $existing) {
    $qc = & sc.exe qc $Name 2>&1 | Out-String
    $serviceConfig = Get-ServiceConfigFromScQc $qc
    if ([string]::IsNullOrWhiteSpace($serviceConfig.StartName)) {
        $safeName = $Name.Replace("'", "''")
        $svc = Get-CimInstance -ClassName Win32_Service -Filter "Name='$safeName'" -ErrorAction SilentlyContinue
        if ($null -ne $svc) {
            $serviceConfig = Get-ServiceConfigFromScQc "SERVICE_START_NAME : $($svc.StartName)`nBINARY_PATH_NAME : $($svc.PathName)"
        }
    }

    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $Name -Force
    }

    sc.exe delete $Name | Out-Host
    Write-Host "Service $Name deleted. The service entry disappears after the next handle close."
    if ($Instance) {
        Write-Host "Instance data was not deleted."
    }
}
elseif (-not $ServiceAccount -or -not $HealthListenUrl) {
    Write-Host "Service $Name is not installed."
    exit 0
}

$accountSource = if ($ServiceAccount) { $ServiceAccount } elseif ($null -ne $serviceConfig) { $serviceConfig.StartName } else { "" }
$urlAclAccount = Resolve-UrlAclAccount -ServiceAccount $accountSource -ServiceName $Name
if (-not $urlAclAccount) {
    return
}

if (-not $HealthListenUrl) {
    $settingsDir = $null
    if ($null -ne $serviceConfig -and $serviceConfig.InstanceDir) {
        $settingsDir = $serviceConfig.InstanceDir
    }
    elseif ($Instance) {
        $settingsDir = Join-Path $InstancesRoot $Instance
    }
    elseif ($null -ne $serviceConfig -and $serviceConfig.InstallDir) {
        $settingsDir = $serviceConfig.InstallDir
    }
    elseif ($InstallPath) {
        $settingsDir = $InstallPath
    }

    if ($settingsDir) {
        $HealthListenUrl = Get-HealthListenUrlFromJson -Path (Join-Path $settingsDir "appsettings.json")
    }
}

$prefix = ConvertTo-HealthListenPrefix $HealthListenUrl
if (-not $prefix) {
    Write-Host "No Health:ListenUrl configured. Skipped removing an HTTP.sys URL reservation."
    return
}

Remove-HttpUrlReservation -Prefix $prefix -Account $urlAclAccount
