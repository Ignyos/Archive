# InnoSetup installer workflow

# Goals/outcomes for the phase (what “done” means).

## Phase1.3 is done when the following have been achieved.

- InnoSetup script created and committed.
	- Installer script stored under /publish (or /scripts) with a clear name
	- Uses the Archive.Desktop build output as input

- Installer metadata is defined.
	- App name: Archive
	- Publisher: Ignyos
	- Version sourced from project or release input
	- Icon configured (from /src/Archive.Desktop/Assets)

- Installation layout and shortcuts are defined.
	- Default install directory (Program Files)
	- Start Menu shortcut
	- Optional Desktop shortcut

- Build pipeline is defined in a script.
	- Refactor release.ps1 for Archive (based on existing Playlist workflow)
	- Script builds Release output and compiles installer
	- Expected installer output location is documented (e.g., /installer)

### Acceptance criteria

- Installer builds without errors

- Installer can be installed and uninstalled cleanly

- App launches from installed shortcut