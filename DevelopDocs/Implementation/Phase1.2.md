# Refactor .vscode Run & Debug scripts

# Goals/outcomes for the phase (what “done” means).

## Phase1.2 is done when the following have been achieved.

- VS Code tasks are standardized for common workflows.
	- Build solution
	- Clean + build
	- Run Archive.Desktop
	- Release script task (if applicable)

- VS Code Run/Debug configurations are updated.
	- Launch Archive.Desktop from the solution root
	- Working directory set to repo root
	- Environment variables documented (if any)

- Tasks and launch configs are documented.
	- Where to run them from
	- Any required prerequisites

### Acceptance criteria

- Build task completes successfully

- Run task launches the WPF app

- Debug configuration attaches and hits breakpoints