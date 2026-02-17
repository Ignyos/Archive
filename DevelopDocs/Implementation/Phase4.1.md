# Main window + job list UX

# Goals/outcomes for the phase (what “done” means).

## Phase4.1 is done when the following have been achieved.

- Main application shell is implemented.
  - Main window opens as the primary desktop entry point
  - Menu actions exist for New Job, Settings, and About
  - Close-to-tray behavior is wired

- Job list is visible and interactive.
  - DataGrid columns match UI spec (Status, Enabled, Name, Description, Next Run, History)
  - Enable/disable toggle updates job state immediately
  - Double-click and context menu actions open expected flows

- Job status presentation is connected.
  - Idle/Scheduled/Running/Warning/Error statuses render from current runtime data
  - Next run is shown for scheduled enabled jobs and empty for manual/disabled jobs

### Acceptance criteria

- Main window loads existing jobs and supports create/edit/delete/run/stop/history entry points

- Status and Next Run values refresh after job changes without restarting the app