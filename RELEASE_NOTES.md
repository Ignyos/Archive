# Release v1.1.0

## Overview
This release introduces the first full desktop experience for Archive, with end-to-end job management, scheduling controls, history visibility, and configurable notifications. It also improves reliability with stronger validation, safer scheduler behavior, and automatic log retention cleanup.

## New Features
- **Desktop Job Management**: Adds a full jobs grid with status, enable/disable toggle, next run visibility, context actions, and double-click editing so you can manage backup jobs from one place.
- **Job Scheduling Controls**: Adds manual, one-time, and recurring triggers with schedule previews, including simple recurring helpers (daily, weekly, monthly) and cron support.
- **Run/Stop/History Actions**: Adds run-now, stop, delete, and history actions directly from the main view for faster day-to-day operations.
- **System Tray Integration**: Adds tray behavior with quick actions, hide-to-tray close behavior, and quick access to schedule and startup options.
- **Settings Window**: Adds configurable startup behavior, notification preferences, and log retention controls so users can tailor Archive to their workflow.
- **Application Log Viewer**: Adds in-app application log viewing to simplify troubleshooting without leaving the app.

## Improvements
- **Scheduling UX**: Improves recurring schedule editing by syncing simple controls with cron expressions and showing upcoming run previews.
- **Execution Feedback**: Improves execution summaries with warning/error details so failures are easier to diagnose.
- **Job Editing Safety**: Improves job-edit validation to prevent invalid trigger settings, duplicate names, and unsafe source/destination path combinations.
- **Notification Experience**: Improves notifications with per-event preferences (start, complete, fail) and deduping/rate-limiting to reduce noisy alerts.

## Bug Fixes
- **Invalid Schedule Guardrails**: Fixes cases where invalid cron or past one-time triggers could be saved, reducing failed or unexpected schedule runs.
- **Path Conflict Protection**: Fixes scenarios where source/destination overlap could create risky sync behavior, helping avoid accidental recursive or destructive outcomes.
- **Database Lock Resilience**: Fixes schedule control startup issues under transient SQLite lock conditions by adding retry behavior.
- **Retention Cleanup Stability**: Fixes long-term log growth by pruning execution/application logs according to retention settings.

## Technical Changes
- Introduces Quartz persistent scheduling with SQLite-backed schema initialization.
- Adds EF Core SQLite persistence for jobs, executions, execution logs, app settings, and application logs.
- Adds job state and scheduling services for enable/disable, soft delete, schedule registration, run-now, and stop flows.
- Adds sync execution pipeline with operation stats capture and completion/failure status publishing.

## Breaking Changes (if any)
- None.

## Installation
- Download and run the Windows installer from the release artifacts.
- Existing installations can be upgraded by running the new installer over the current version.

## Requirements
- Windows 10/11 (64-bit)
- .NET 9 runtime support on target machine

## Documentation
- Product docs: https://archive.ignyos.com/
- Repository guides: `RELEASE_WORKFLOW.md`, `docs/getting-started.html`, `docs/troubleshooting.html`

