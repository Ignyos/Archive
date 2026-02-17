# System tray + settings + notifications

# Goals/outcomes for the phase (what “done” means).

## Phase4.5 is done when the following have been achieved.

- System tray behavior is fully wired.
  - App can be opened/restored from tray icon
  - Tray menu supports scheduler running toggle
  - Tray menu supports run-on-startup and clean shutdown actions

- Settings dialog is implemented with persistence.
  - General, Notifications, and Advanced sections are present
  - Settings auto-save on change
  - App-wide settings are loaded on startup and applied at runtime

- Notification controls affect behavior.
  - Global notification enable/disable is respected
  - Start/complete/fail notification preferences are honored
  - Notification sound preference is honored when enabled

### Acceptance criteria

- Changing scheduler toggle in tray immediately pauses/resumes scheduled triggers without closing the app

- Settings changes persist across app restart and affect subsequent job executions

## Implementation tasks (suggested breakdown)

- Tray icon + menu behaviors
  - Add tray icon lifecycle (create, show, dispose)
  - Implement Open Archive command and focus/restore behavior
  - Implement Shut down Archive command with clean app exit path

- Scheduler toggle integration
  - Bind tray Scheduler Running checkbox to global schedule state
  - Pause/resume triggers through scheduler service when toggled
  - Reflect state changes in both tray and main UI

- Startup integration
  - Implement Run on Windows Startup setting read/write behavior
  - Register/unregister startup entry safely
  - Show persisted startup state on load

- Settings dialog persistence
  - Build General, Notifications, and Advanced settings sections
  - Auto-save changes through app settings service
  - Load settings at startup and apply at runtime

- Notification behavior wiring
  - Gate all notifications behind global enable flag
  - Honor start/complete/fail preference switches
  - Honor notification sound preference when notifications are enabled