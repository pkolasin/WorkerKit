# Reads workerkit.app.json from the publish directory (the parent of deploy/).
# Windows PowerShell 5.1: no ternary, no && / ||, no ??.

function Get-WorkerKitApp {
    $publishDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $manifest = Join-Path $publishDir "workerkit.app.json"
    if (-not (Test-Path -LiteralPath $manifest)) {
        throw "Missing $manifest. Publish the connector so WorkerKit writes workerkit.app.json next to the app."
    }

    $app = Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $app) {
        throw "workerkit.app.json at $manifest is empty."
    }

    if ([string]::IsNullOrWhiteSpace([string] $app.serviceName) -or [string]::IsNullOrWhiteSpace([string] $app.windowsExecutable)) {
        throw "workerkit.app.json at $manifest is missing serviceName or windowsExecutable."
    }

    return $app
}

# $true only when the pid file names a running default-instance process of this install's exe.
# A named instance (--instance-dir on the command line) is not the default instance.
# A missing file is not a trace. Anything else is a stale file: warn and return $false.
function Test-WorkerKitLiveInstancePid {
    param(
        [string] $PidFile,
        [string] $ExePath
    )

    if ([string]::IsNullOrWhiteSpace($PidFile) -or -not (Test-Path -LiteralPath $PidFile)) {
        return $false
    }

    $text = Get-Content -LiteralPath $PidFile -Raw -ErrorAction SilentlyContinue
    if ($null -eq $text) {
        Write-Warning "Ignoring stale instance.pid at $PidFile (it could not be read)."
        return $false
    }

    $processId = 0
    if (-not [int]::TryParse($text.Trim(), [ref] $processId) -or $processId -le 0) {
        Write-Warning "Ignoring stale instance.pid at $PidFile (not a process id)."
        return $false
    }

    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        Write-Warning "Ignoring stale instance.pid at $PidFile (process $processId is not running)."
        return $false
    }

    $processPath = $null
    $pathProperty = $process.PSObject.Properties['Path']
    if ($null -ne $pathProperty -and -not [string]::IsNullOrWhiteSpace([string] $pathProperty.Value)) {
        $processPath = [string] $pathProperty.Value
    }

    if ([string]::IsNullOrWhiteSpace($processPath)) {
        try {
            $processPath = $process.MainModule.FileName
        }
        catch {
            $processPath = $null
        }
    }

    if ([string]::IsNullOrWhiteSpace($processPath)) {
        Write-Warning "Ignoring stale instance.pid at $PidFile (could not read the path of process $processId)."
        return $false
    }

    $expected = [System.IO.Path]::GetFullPath($ExePath)
    $actual = [System.IO.Path]::GetFullPath($processPath)
    if (-not $actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Warning "Ignoring stale instance.pid at $PidFile (process $processId is '$actual', not '$expected')."
        return $false
    }

    $commandLine = Get-WorkerKitProcessCommandLine $processId
    if ($commandLine -like "*--instance-dir*") {
        Write-Warning "Ignoring instance.pid at $PidFile (process $processId was started with --instance-dir)."
        return $false
    }

    return $true
}

function Get-WorkerKitProcessCommandLine {
    param([int] $ProcessId)

    $procCmd = "/proc/$ProcessId/cmdline"
    if (Test-Path -LiteralPath $procCmd) {
        $raw = [System.IO.File]::ReadAllBytes($procCmd)
        return [System.Text.Encoding]::UTF8.GetString($raw).Replace([string][char]0, " ")
    }

    try {
        $cim = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction SilentlyContinue
        if ($null -ne $cim) {
            return [string] $cim.CommandLine
        }
    }
    catch {
        return $null
    }

    return $null
}

# Empty service or trace lists print "none", not a blank between the colon and the semicolon.
function Format-WorkerKitDefaultInstanceRefusal {
    param(
        [string] $InstallPath,
        [string[]] $ServiceNames,
        [string[]] $Traces
    )

    $serviceText = "none"
    $names = @($ServiceNames | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($names.Count -gt 0) { $serviceText = $names -join ", " }
    $traceText = "none"
    $traceNames = @($Traces | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($traceNames.Count -gt 0) { $traceText = $traceNames -join ", " }
    return ("Instance 'default' (configuration in $InstallPath) is in use: " +
            "services without --instance-dir: $serviceText; traces: $traceText. " +
            "A single-instance install is updated manually. Use -DefaultInstance to update binaries only and keep appsettings*.json.")
}
