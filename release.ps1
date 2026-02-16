# Release Script for Archive
# Handles version bumping, building, and releasing

$ErrorActionPreference = "Stop"

# Colors
$InfoColor = "Cyan"
$SuccessColor = "Green"
$WarningColor = "Yellow"
$ErrorColor = "Red"

function Get-CurrentVersion {
    $csprojPath = "src\Archive.Desktop\Archive.Desktop.csproj"
    $content = Get-Content $csprojPath -Raw
    if ($content -match '<Version>([\d\.]+)</Version>') {
        return $matches[1]
    }
    throw "Could not find version in $csprojPath"
}

function Set-ProjectVersion {
    param([string]$NewVersion)
    
    $csprojPath = "src\Archive.Desktop\Archive.Desktop.csproj"
    $content = Get-Content $csprojPath -Raw
    
    # Update all version fields
    $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$NewVersion</Version>"
    $content = $content -replace '<AssemblyVersion>[\d\.]+\.0</AssemblyVersion>', "<AssemblyVersion>$NewVersion.0</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[\d\.]+\.0</FileVersion>', "<FileVersion>$NewVersion.0</FileVersion>"
    
    $content | Set-Content $csprojPath -NoNewline
    Write-Host "Updated version to $NewVersion in Archive.Desktop.csproj" -ForegroundColor $SuccessColor
}

function Build-LocalInstaller {
    param(
        [string]$Version,
        [bool]$IsDevBuild = $false
    )
    
    Write-Host "`nBuilding local installer for version $Version..." -ForegroundColor $InfoColor
    
    # Clean previous build
    if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }
    
    # Clean only the release installer, keep debug installers with timestamps
    if (Test-Path "installer\ArchiveSetup.exe") { 
        Remove-Item "installer\ArchiveSetup.exe" -Force
    }
    
    # Publish
    Write-Host "Publishing application..." -ForegroundColor $InfoColor
    dotnet publish src\Archive.Desktop\Archive.Desktop.csproj --configuration Release --runtime win-x64 --self-contained --output .\publish /p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    
    # Create installer directory
    New-Item -ItemType Directory -Force -Path .\installer | Out-Null
    
    # Update InnoSetup script
    Write-Host "Updating InnoSetup configuration..." -ForegroundColor $InfoColor
    $setupPath = "scripts\ArchiveSetup.iss"
    $setupContent = Get-Content $setupPath -Raw
    $setupContent = $setupContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""
    
    # Set installer filename based on build type
    if ($IsDevBuild) {
        $timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
        $installerName = "ArchiveSetup_$timestamp"
    } else {
        $installerName = "ArchiveSetup"
    }
    $setupContent = $setupContent -replace 'OutputBaseFilename=.*', "OutputBaseFilename=$installerName"
    $setupContent | Set-Content $setupPath -NoNewline
    
    # Compile installer
    Write-Host "Compiling installer with InnoSetup..." -ForegroundColor $InfoColor
    $innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $innoSetupPath)) {
        throw "InnoSetup not found at $innoSetupPath"
    }
    
    & $innoSetupPath $setupPath
    if ($LASTEXITCODE -ne 0) {
        throw "Installer compilation failed"
    }
    
    # Cleanup publish folder
    Write-Host "Cleaning up build artifacts..." -ForegroundColor $InfoColor
    if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }
    
    Write-Host "`nInstaller created successfully: installer\$installerName.exe" -ForegroundColor $SuccessColor
}

function Revert-VersionChange {
    param([string]$OriginalVersion)
    
    Write-Host "Reverting version changes..." -ForegroundColor $WarningColor
    git checkout -- src\Archive.Desktop\Archive.Desktop.csproj 2>$null
    Write-Host "Version reverted to $OriginalVersion" -ForegroundColor $WarningColor
}

# Main script
Write-Host "=== Archive Release Script ===" -ForegroundColor $InfoColor
Write-Host ""

# Check current branch
$currentBranch = git rev-parse --abbrev-ref HEAD
$isMainBranch = $currentBranch -eq "main"

Write-Host "Current branch: $currentBranch" -ForegroundColor $(if ($isMainBranch) { $SuccessColor } else { $WarningColor })
if (-not $isMainBranch) {
    Write-Host "Note: Tags will only be created and pushed from the main branch" -ForegroundColor $WarningColor
}
Write-Host ""

# Get current version
$currentVersion = Get-CurrentVersion
Write-Host "Current version: $currentVersion" -ForegroundColor $InfoColor

# Prompt for new version
$newVersionInput = Read-Host "New version [$currentVersion]"
$newVersion = if ([string]::IsNullOrWhiteSpace($newVersionInput)) { $currentVersion } else { $newVersionInput }

# If version unchanged, build local installer and exit
if ($newVersion -eq $currentVersion) {
    $timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
    $localVersion = "$currentVersion-$timestamp"
    Write-Host "`nBuilding local installer (no version change)..." -ForegroundColor $InfoColor
    
    try {
        Build-LocalInstaller -Version $localVersion -IsDevBuild $true
        Write-Host "`nLocal build complete!" -ForegroundColor $SuccessColor
        
        # Revert InnoSetup script changes
        Write-Host "Reverting ArchiveSetup.iss..." -ForegroundColor $InfoColor
        git checkout -- scripts\ArchiveSetup.iss 2>$null
    }
    catch {
        Write-Host "Build failed: $_" -ForegroundColor $ErrorColor
        git checkout -- scripts\ArchiveSetup.iss 2>$null
        exit 1
    }
    
    exit 0
}

# Validate new version format
if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Invalid version format. Use X.Y.Z (e.g., 1.0.1)" -ForegroundColor $ErrorColor
    exit 1
}

# Generate diff for release notes BEFORE making changes
$timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
$diffFile = "rc_${newVersion}_${timestamp}.txt"
Write-Host "`nGenerating diff for release notes..." -ForegroundColor $InfoColor

# Clear RELEASE_NOTES.md so it only contains notes for the new release
Write-Host "Clearing RELEASE_NOTES.md to start fresh..." -ForegroundColor $InfoColor
"" | Set-Content "RELEASE_NOTES.md" -Encoding utf8

# Get last tag, or use empty tree if no tags exist (first release)
$lastTag = git describe --tags --abbrev=0 2>&1 | Where-Object { $_ -is [string] }
if ($lastTag) {
    Write-Host "Comparing changes since $lastTag..." -ForegroundColor $InfoColor
    git diff "$lastTag..HEAD" | Out-File -FilePath $diffFile -Encoding utf8
} else {
    Write-Host "No previous tags found, showing all changes..." -ForegroundColor $InfoColor
    $emptyTree = git hash-object -t tree /dev/null
    git diff "$emptyTree..HEAD" | Out-File -FilePath $diffFile -Encoding utf8
}

Write-Host "`nDiff saved to $diffFile" -ForegroundColor $SuccessColor

# Generate AI prompt
$aiPrompt = @"
Please update RELEASE_NOTES.md for version $newVersion using the content from $diffFile

Follow the style and format defined in RELEASE_NOTES_STYLE.md.

Focus on:
- User-facing changes and benefits
- New features and improvements
- Bug fixes and their impact
- Breaking changes (if any)

The raw git diff is in $diffFile - transform it into clear, user-friendly release notes.

File locations:
- Source diff: $diffFile
- Target file: RELEASE_NOTES.md
- Style guide: RELEASE_NOTES_STYLE.md
"@

# Copy prompt to clipboard
try {
    Set-Clipboard -Value $aiPrompt
    Write-Host "`n✓ AI prompt copied to clipboard!" -ForegroundColor $SuccessColor
    Write-Host "  Paste it into your AI tool to generate release notes." -ForegroundColor $InfoColor
}
catch {
    Write-Host "`nAI Prompt (copy manually if clipboard failed):" -ForegroundColor $WarningColor
    Write-Host $aiPrompt -ForegroundColor Gray
}

Write-Host "`nWhen AI has updated RELEASE_NOTES.md, press any key to continue..." -ForegroundColor $WarningColor
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Prompt to continue or cancel
Write-Host ""
$choice = Read-Host "Continue with release? (y/[n])"

if ($choice -notmatch '^y(es)?$') {
    Write-Host "Release cancelled" -ForegroundColor $WarningColor
    # Remove the rc_ file since we're cancelling
    if (Test-Path $diffFile) { Remove-Item $diffFile }
    exit 0
}

# Validate RELEASE_NOTES.md file
if (-not (Test-Path "RELEASE_NOTES.md")) {
    Write-Host "RELEASE_NOTES.md not found. Please create it with your release notes." -ForegroundColor $ErrorColor
    exit 1
}

$releaseNotes = Get-Content "RELEASE_NOTES.md" -Raw
if ([string]::IsNullOrWhiteSpace($releaseNotes)) {
    Write-Host "RELEASE_NOTES.md is empty. Please add release notes." -ForegroundColor $ErrorColor
    exit 1
}

# Update version in csproj
try {
    Set-ProjectVersion -NewVersion $newVersion
}
catch {
    Write-Host "Failed to update version: $_" -ForegroundColor $ErrorColor
    exit 1
}

# Build installer
Write-Host "`nBuilding installer..." -ForegroundColor $InfoColor
try {
    Build-LocalInstaller -Version $newVersion -IsDevBuild $false
}
catch {
    Write-Host "Build failed: $_" -ForegroundColor $ErrorColor
    Write-Host "Reverting changes..." -ForegroundColor $WarningColor
    Revert-VersionChange -OriginalVersion $currentVersion
    exit 1
}

# Commit changes using RELEASE_NOTES.md
Write-Host "\nCommitting changes..." -ForegroundColor $InfoColor
git add .
git commit -F "RELEASE_NOTES.md"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed" -ForegroundColor $ErrorColor
    exit 1
}

Write-Host "Changes committed successfully" -ForegroundColor $SuccessColor

# Create and push tag (only on main branch)
if ($isMainBranch) {
    $tag = "v$newVersion"
    Write-Host "`nCreating tag: $tag" -ForegroundColor $InfoColor
    git tag -a $tag -m "Release $newVersion"
    
    Write-Host "Pushing commit and tag to origin..." -ForegroundColor $InfoColor
    git push origin $currentBranch
    git push origin $tag
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Push failed" -ForegroundColor $ErrorColor
        exit 1
    }
    
    Write-Host "`nRelease complete! Tag $tag pushed to GitHub." -ForegroundColor $SuccessColor
    Write-Host "GitHub Actions will build and publish the release." -ForegroundColor $InfoColor
}
else {
    Write-Host "`nChanges committed locally (no tag created - not on main branch)" -ForegroundColor $SuccessColor
}

Write-Host "`nDone!" -ForegroundColor $SuccessColor
