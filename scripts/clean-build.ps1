$ErrorActionPreference = 'Stop'

function Write-Status {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $foregroundColor = switch ($Level) {
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
        default { 'Cyan' }
    }

    Write-Host $Message -ForegroundColor $foregroundColor
}

$archiveProcesses = Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -ieq 'Archive.Desktop.exe') -or
    ($_.Name -ieq 'dotnet.exe' -and $_.CommandLine -like '*Archive.Desktop.dll*')
}

foreach ($process in $archiveProcesses) {
    try {
        Write-Status -Message "Stopping process: $($process.Name) (PID: $($process.ProcessId))" -Level Info
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
    }
    catch {
        Write-Status -Message "Failed to stop process: $($process.Name) (PID: $($process.ProcessId))" -Level Warning
    }
}

$buildArtifacts = Get-ChildItem -Path . -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Sort-Object { $_.FullName.Length } -Descending

$previousProgressPreference = $ProgressPreference
$ProgressPreference = 'SilentlyContinue'
try {
    foreach ($artifactDirectory in $buildArtifacts) {
        try {
            Write-Status -Message "Removing build artifact: $($artifactDirectory.FullName)" -Level Info
            Remove-Item -Path $artifactDirectory.FullName -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Status -Message "Failed to remove build artifact: $($artifactDirectory.FullName)" -Level Warning
        }
    }
}
finally {
    $ProgressPreference = $previousProgressPreference
}

$maxBuildAttempts = 4

for ($attempt = 1; $attempt -le $maxBuildAttempts; $attempt++) {
    Write-Status -Message "Build attempt $attempt of $maxBuildAttempts..." -Level Info
    $buildOutput = dotnet build Archive.sln --configuration Debug /nodeReuse:false -m:1 /p:UseSharedCompilation=false 2>&1
    $buildExitCode = if ($null -ne $LASTEXITCODE) { [int]$LASTEXITCODE } else { -1 }
    $buildOutput | ForEach-Object { Write-Host $_ }

    if ($buildExitCode -eq 0) {
        Write-Status -Message "Build succeeded." -Level Success
        break
    }

    $isFileLockFailure = ($buildOutput | Out-String) -match 'MSB3491|being used by another process|cannot access the file'
    if (-not $isFileLockFailure -or $attempt -eq $maxBuildAttempts) {
        Write-Status -Message ("Build failed with exit code {0}." -f $buildExitCode) -Level Error
        throw ("Build failed with exit code {0}." -f $buildExitCode)
    }

    Write-Status -Message "Detected likely file lock issue; retrying build after backoff." -Level Warning
    Start-Sleep -Seconds ([Math]::Min(6, $attempt * 2))
}
