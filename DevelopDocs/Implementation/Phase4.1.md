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

## Implementation tasks (suggested breakdown)

- Main window shell
  - Create main window layout regions (menu, list, status bar)
  - Wire menu commands for New Job, Settings, About
  - Implement close-to-tray window behavior

- Job list view model + bindings
  - Define row model for status, enabled, name, description, next run
  - Bind DataGrid columns and command actions
  - Add data refresh trigger after job mutations

- Row interactions
  - Implement double-click to open Edit Job
  - Implement context menu commands (Edit, Delete, Run Now, Stop, View History)
  - Add History hyperlink navigation

- Runtime status projection
  - Map scheduler/runtime state to Idle/Scheduled/Running/Warning/Error
  - Compute Next Run visibility rules (scheduled vs manual/disabled)
  - Add lightweight polling or event-driven status refresh