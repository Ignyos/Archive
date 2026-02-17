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