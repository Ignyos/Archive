# Job history + execution details UX

# Goals/outcomes for the phase (what “done” means).

## Phase4.4 is done when the following have been achieved.

- Job History window is implemented.
  - History opens from the main list and shows execution records per job
  - Columns match baseline metrics (run time, duration, scanned/copied/updated/deleted/failed)
  - Default ordering is newest-first

- Job Details window is implemented.
  - Execution summary shows status, duration, and counters
  - Copy/Update/Delete/Failed/Skipped sections render operation-level details
  - Failed section expands by default when failures exist

- Data mapping from persistence is consistent.
  - JobExecution and ExecutionLog data drive visible history/details output
  - Status labels align with Completed/CompletedWithWarnings/Failed/Cancelled semantics

### Acceptance criteria

- User can open any historical run for a job and inspect summary + operation sections

- Warnings and errors are visible with enough detail to diagnose failed/skipped files