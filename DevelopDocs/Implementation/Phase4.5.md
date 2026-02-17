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