$ErrorActionPreference = 'Stop'

$archiveProcesses = Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -ieq 'Archive.Desktop.exe') -or
    ($_.Name -ieq 'dotnet.exe' -and $_.CommandLine -like '*Archive.Desktop.dll*')
}

foreach ($process in $archiveProcesses) {
    try {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
    }
    catch {
    }
}

$buildArtifacts = Get-ChildItem -Path . -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($artifactDirectory in $buildArtifacts) {
    try {
        Remove-Item -Path $artifactDirectory.FullName -Recurse -Force -ErrorAction Stop
    }
    catch {
    }
}

$maxBuildAttempts = 4

for ($attempt = 1; $attempt -le $maxBuildAttempts; $attempt++) {
    $buildOutput = dotnet build Archive.sln --configuration Debug /nodeReuse:false -m:1 /p:UseSharedCompilation=false 2>&1
    $buildOutput | ForEach-Object { Write-Host $_ }

    if ($LASTEXITCODE -eq 0) {
        exit 0
    }

    $isFileLockFailure = ($buildOutput | Out-String) -match 'MSB3491|being used by another process|cannot access the file'
    if (-not $isFileLockFailure -or $attempt -eq $maxBuildAttempts) {
        throw "Build failed with exit code $LASTEXITCODE."
    }

    Start-Sleep -Seconds ([Math]::Min(6, $attempt * 2))
}
