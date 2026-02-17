# Scheduling integration (Quartz + manual)

# Goals/outcomes for the phase (what “done” means).

## Phase3.4 is done when the following have been achieved.

- Quartz is configured and running.
  - Persistent store (SQLite)
  - Job registration by JobId

- Manual execution is supported.
  - Run Now triggers immediate execution

- Basic scheduling works.
  - One-time and recurring triggers
  - Misfire policy set to DoNothing

### Acceptance criteria

- Scheduled job triggers execution

- Manual run bypasses schedule and executes immediately
