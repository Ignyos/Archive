# Phase 4 recommended execution order

This document converts Phase 4 task lists into a dependency-aware implementation sequence.

## Implementation snapshot (2026-02-19)

- Wave 1: Completed
- Wave 2: Completed
- Wave 3: Completed
- Wave 4: Completed
- Wave 5: Completed

Completed Wave 5 items:
- 17) Recurring Advanced + Cron tab synchronization
- 18) Scheduler toggle integration (tray + main UI)
- 19) Settings persistence (General/Notifications/Advanced with auto-save)
- 20) Startup integration and notification preference wiring
	- Includes startup migration + retention prune, startup registration behavior, and per-event notification preference resolution.
	- Notification delivery uses stable tray notifications with rate limiting/deduplication for the current target.

## Ordering principles

- Build core navigation and data surfaces first.
- Implement Create/Edit and scheduling before history/details, because they generate and shape runtime data.
- Integrate tray/settings early enough to test scheduler state behavior during UI development.
- Reserve advanced scheduling builder and polish tasks for later iterations after baseline paths are stable.

## Recommended sequence (by wave)

### Wave 1 — Foundation and navigation

Primary goal: app shell + essential entry points.

1) Phase4.1: Main window shell
- Create main window layout regions (menu, list, status bar)
- Wire menu commands for New Job, Settings, About
- Implement close-to-tray behavior

2) Phase4.1: Job list view model + bindings (baseline)
- Define row model for status, enabled, name, description, next run
- Bind DataGrid columns
- Add placeholder command bindings for row actions

3) Phase4.5: Tray icon + menu behaviors (baseline)
- Add tray icon lifecycle (create/show/dispose)
- Implement Open Archive and Shut down Archive actions

Exit criteria:
- App boots into main window, tray behavior works, and UI navigation entry points are reachable.

---

### Wave 2 — Job authoring baseline

Primary goal: users can create/edit valid jobs end to end.

4) Phase4.2: Dialog composition
- Build Create/Edit modal sections and control set
- Load create defaults and edit-mode persisted values

5) Phase4.2: Validation layer
- Required/name uniqueness/path relationship validation
- Inline messages + Preview/OK enablement rules

6) Phase4.2: Destructive option safeguards
- Delete-orphaned confirmation flow (Yes/No/Preview)

7) Phase4.1: Row interactions
- Double-click edit, context menu actions, History link navigation

Exit criteria:
- Valid jobs can be created/edited and reflected in the main list; invalid jobs are blocked with clear feedback.

---

### Wave 3 — Scheduling integration (MVP)

Primary goal: robust manual/one-time/recurring scheduling UX with persistence.

8) Phase4.3: Trigger mode switching
- Manual/One-Time/Recurring mode controls + mode validation

9) Phase4.3: One-Time scheduling UX
- Future DateTime picker, preview text, persistence rules

10) Phase4.3: Recurring Simple tab
- Hourly/daily/weekly/monthly presets + cron generation

11) Phase4.3: Schedule preview + persistence
- Next-run preview computation
- Persist schedule fields
- Verify scheduler receives updated triggers after save

12) Phase4.1: Runtime status projection
- Map runtime state to list statuses and Next Run display

Exit criteria:
- Scheduling changes persist, expected runs occur, and main list status/next-run fields track current scheduler state.

---

### Wave 4 — Observability and diagnostics

Primary goal: make execution outcomes inspectable and actionable.

13) Phase4.4: Job History window foundation
- Non-modal history window, required columns, default sort

14) Phase4.4: Job Details window foundation
- Run summary + collapsible operation sections

15) Phase4.4: Data projection and formatting
- JobExecution/ExecutionLog mapping and timestamp/duration formatting

16) Phase4.4: State and navigation behavior
- Single-instance history/details windows + empty states

Exit criteria:
- Users can open run history/details and diagnose warnings/errors without checking raw logs.

---

### Wave 5 — Advanced scheduling UX + runtime controls hardening

Primary goal: complete advanced UX and operational polish.

17) Phase4.3: Recurring Advanced + Cron tabs
- Advanced builder UI, cron parser validation, cross-tab synchronization

18) Phase4.5: Scheduler toggle integration
- Bind Scheduler Running toggle to global schedule state
- Pause/resume triggers and reflect state in tray + main UI

19) Phase4.5: Settings dialog persistence
- General/Notifications/Advanced sections with auto-save
- Load/apply settings at runtime and startup

20) Phase4.5: Startup integration + notification wiring
- Startup registration behavior
- Global and per-event notification preference handling

Exit criteria:
- Advanced scheduling flows are stable, global runtime controls are reliable, and settings behavior persists correctly across restarts.

## Parallelization guidance

- Lane A (Main UI): Phase4.1 tasks 1,2,7,12
- Lane B (Job authoring): Phase4.2 tasks 4,5,6
- Lane C (Scheduling): Phase4.3 tasks 8,9,10,11,17
- Lane D (History/details): Phase4.4 tasks 13,14,15,16
- Lane E (Tray/settings): Phase4.5 tasks 3,18,19,20

Recommended pairing:
- Wave 1: A + E (baseline)
- Wave 2: B + A (interactions)
- Wave 3: C + A (status projection)
- Wave 4: D
- Wave 5: C (advanced) + E (hardening)

## Suggested milestone checkpoints

- Milestone M1: End of Wave 1 (navigable shell + tray baseline)
- Milestone M2: End of Wave 2 (job create/edit usable)
- Milestone M3: End of Wave 3 (MVP scheduling complete)
- Milestone M4: End of Wave 4 (history/details usable)
- Milestone M5: End of Wave 5 (feature-complete Phase 4)