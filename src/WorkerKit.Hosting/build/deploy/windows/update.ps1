# Updates binaries in a shared install directory. Every instance of that install must be stopped
# first; Windows will not replace DLLs that are still loaded.
# Install path, instance root, and the executable name come from workerkit.app.json,
# the same defaults install-service.ps1 and uninstall-service.ps1 use.
#
# A single-instance install (instance "default": configuration lives in the install directory)
# is refused unless -DefaultInstance is set. With that switch only binaries are copied;
# appsettings*.json and instance data stay.
param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [string] $InstallPath,
    [string] $InstancesRoot,
    [switch] $DefaultInstance
)

$ErrorActionPreference = "Stop"
# robocopy uses exit codes 0-7 for success. PowerShell 7 must not treat those as errors.
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue) {
    $Global:PSNativeCommandUseErrorActionPreference = $false
}

. (Join-Path $PSScriptRoot "WorkerKitApp.ps1")
$app = Get-WorkerKitApp
if (-not $InstallPath) { $InstallPath = [string] $app.windowsInstallPath }
if (-not $InstancesRoot) { $InstancesRoot = [Environment]::ExpandEnvironmentVariables([string] $app.windowsInstancesRoot) }

$exe = Join-Path $InstallPath ([string] $app.windowsExecutable)
# Services whose binPath points at this install directory, including ones that were renamed by hand.
$services = @(Get-CimInstance Win32_Service | Where-Object { $_.PathName -like "*$exe*" })
$withInst = @($services | Where-Object { $_.PathName -match '--instance(-dir)?\b' })
$defaultSvc = @($services | Where-Object { $_.PathName -notmatch '--instance(-dir)?\b' })
$defaultTrace = New-Object System.Collections.Generic.List[string]
foreach ($name in @(".lock", "logs", "data", "temp")) {
    if (Test-Path (Join-Path $InstallPath $name)) { $defaultTrace.Add($name) }
}

# A pid file left after a crash counts only while that process is still this install's executable.
if (Test-WorkerKitLiveInstancePid -PidFile (Join-Path $InstallPath "instance.pid") -ExePath $exe) {
    $defaultTrace.Add("instance.pid")
}
$noInstances = $withInst.Count -eq 0 -and -not (Get-ChildItem $InstancesRoot -Directory -ErrorAction SilentlyContinue)
$usesDefault = $defaultSvc.Count -gt 0 -or $defaultTrace.Count -gt 0 -or $noInstances

if ($usesDefault -and -not $DefaultInstance) {
    $serviceNames = @($defaultSvc | ForEach-Object { $_.Name })
    throw (Format-WorkerKitDefaultInstanceRefusal -InstallPath $InstallPath -ServiceNames $serviceNames -Traces @($defaultTrace))
}

$running = @($services | Where-Object State -eq "Running")
foreach ($s in $running) { Stop-Service -Name $s.Name -Force }
foreach ($s in $running) { Wait-Process -Id $s.ProcessId -Timeout 30 -ErrorAction SilentlyContinue }

$exclude = @("/XF", ".lock", "instance.pid")
if ($DefaultInstance) { $exclude = @("/XD", "logs", "data", "temp", "/XF", ".lock", "instance.pid", "appsettings*.json") }
robocopy $PublishDir $InstallPath /MIR @exclude /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }

& $exe version
if ($LASTEXITCODE -ne 0) { throw "version smoke test failed ($LASTEXITCODE)" }

foreach ($s in $running) { Start-Service -Name $s.Name }
