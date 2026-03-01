$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Path $PSScriptRoot -Parent

& (Join-Path $PSScriptRoot 'clean-build.ps1')

$desktopOutputDirectory = Join-Path $repoRoot 'src/Archive.Desktop/bin/Debug/net9.0-windows'
$desktopExecutablePath = Join-Path $desktopOutputDirectory 'Archive.Desktop.exe'

if (-not (Test-Path -Path $desktopExecutablePath)) {
    throw "Unable to find desktop executable at '$desktopExecutablePath'."
}

Write-Host "Launching Archive.Desktop from: $desktopExecutablePath" -ForegroundColor Cyan

$process = Start-Process -FilePath $desktopExecutablePath -WorkingDirectory $desktopOutputDirectory -PassThru
Start-Sleep -Milliseconds 500

if ($process.HasExited) {
    throw "Archive.Desktop exited immediately with exit code $($process.ExitCode)."
}

Write-Host "Archive.Desktop started successfully (PID: $($process.Id)). Waiting for it to exit..." -ForegroundColor Green
Wait-Process -Id $process.Id