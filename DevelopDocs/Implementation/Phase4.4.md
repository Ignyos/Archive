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

## Implementation tasks (suggested breakdown)

- Job History window foundation
  - Build non-modal history window with job metadata header
  - Bind execution list with required columns and default sort
  - Add details navigation command per row

- Job Details window foundation
  - Build summary header for run status, duration, and counters
  - Implement collapsible operation sections
  - Set default expansion behavior for Failed section when failures exist

- Data projection and formatting
  - Map JobExecution fields to UI summary and list columns
  - Map ExecutionLog entries to operation-grouped sections
  - Format duration and timestamps consistently across windows

- State and navigation behavior
  - Enforce single-instance behavior per history/details window type
  - Support opening details from selected history record
  - Handle empty states (no runs, no failures, no skipped files)