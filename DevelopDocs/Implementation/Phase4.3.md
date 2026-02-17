# Scheduling UX (manual, one-time, recurring)

# Goals/outcomes for the phase (what “done” means).

## Phase4.3 is done when the following have been achieved.

- Trigger-type-specific scheduling UX is implemented.
  - Manual mode requires no schedule controls
  - One-Time mode supports future date/time selection
  - Recurring mode supports schedule builder + cron input

- Recurring scheduling experiences are connected.
  - Simple presets generate valid Quartz expressions
  - Advanced builder generates and updates cron text
  - Cron tab validates expressions in real time and surfaces parsing errors

- Cross-tab behavior and feedback are stable.
  - Advanced Builder and Cron tabs synchronize when expressions are parseable
  - UI shows user-friendly next-run preview for configured schedules

### Acceptance criteria

- Saving recurring jobs persists valid cron values and schedules run at expected times

- Invalid one-time or cron input is blocked and clearly explained in the dialog

## Implementation tasks (suggested breakdown)

- Trigger mode switching
  - Implement Manual, One-Time, and Recurring mode selectors
  - Show/hide mode-specific controls with state preservation rules
  - Add mode-specific validation messages

- One-Time scheduling UX
  - Add future DateTime picker and user-friendly preview text
  - Validate future-only constraint in real time
  - Persist one-time value and clear conflicting cron fields

- Recurring Simple tab
  - Implement presets (hourly, daily, weekly, monthly)
  - Generate cron from simple controls
  - Validate weekly/monthly special constraints

- Recurring Advanced + Cron tabs
  - Build Advanced Builder controls and generated expression output
  - Add cron text validation using Quartz parser
  - Synchronize Advanced/Cron views when expressions are parseable

- Schedule preview + persistence
  - Compute and display next-run preview values
  - Persist selected scheduling mode + expression/time fields
  - Verify scheduler receives updated trigger definitions after save