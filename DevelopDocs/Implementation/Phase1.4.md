# Refactor .github/workflows/release.yml

# Goals/outcomes for the phase (what “done” means).

## Phase1.4 is done when the following have been achieved.

- Release workflow targets Archive.Desktop.
	- Uses src/Archive.Desktop/Archive.Desktop.csproj
	- Publishes Windows x64 self-contained build

- Inno Setup is integrated correctly.
	- Installs Inno Setup via Chocolatey
	- Uses scripts/ArchiveSetup.iss
	- Outputs ArchiveSetup.exe

- Release assets are published.
	- Upload installer artifact
	- (Optional) Upload portable EXE if produced

- Release notes are sourced from repository.
	- Uses RELEASE_NOTES.md

### Acceptance criteria

- Workflow runs on version tag (vX.Y.Z)

- Installer is created and attached to GitHub Release

- Release notes appear in GitHub Release