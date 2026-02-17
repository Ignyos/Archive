# Sync engine core

# Goals/outcomes for the phase (what “done” means).

## Phase3.1 is done when the following have been achieved.

- Core sync pipeline exists.
  - Scan source tree
  - Compare source vs destination (Fast + Accurate)
  - Plan operations (copy/update/delete)

- File operations are implemented.
  - Copy new files
  - Update existing files
  - Delete in mirror mode

- Sync options are honored.
  - Recursive
  - DeleteOrphaned
  - VerifyAfterCopy (optional)
  - SkipHiddenAndSystem

### Acceptance criteria

- Unit tests for scan/compare/planning pass

- Manual run can copy/update a sample folder
