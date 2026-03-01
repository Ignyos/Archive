# Release v1.0.0

## Overview
Archive 1.0.0 is the first stable release of the desktop backup experience. It delivers reliable scheduled backups, safer preview workflows, and clearer run visibility so you can trust what will happen before and after each job.

## New Features
- **Desktop Backup Manager**: Adds a full Windows desktop app to create, edit, run, and monitor backup jobs in one place.
- **Flexible Scheduling Modes**: Adds **Manual**, **One-Time**, and **Recurring** scheduling so you can run backups on demand or automate them.
- **Schedule Preview**: Adds next-run preview feedback for recurring and one-time schedules so schedule mistakes are easier to catch before saving.
- **Preview Operations (Dry Run)**: Adds pre-run operation previews showing files to add/update/delete, skipped files, and estimated transfer size.
- **Job History and Execution Details**: Adds historical run tracking with per-run statistics and detailed operation-level results.
- **Application Logs View**: Adds an in-app application logs window for faster diagnostics when troubleshooting.

## Improvements
- **Settings Experience**: Adds a dedicated Settings window with auto-save for startup behavior, notifications, and log retention.
- **System Tray Controls**: Improves background operation with tray actions for showing the app, toggling scheduler state, and exiting cleanly.
- **Status and Next-Run Clarity**: Improves job list status and next-run visibility so manual, disabled, and scheduled jobs are easier to distinguish.
- **Safer Job Configuration**: Improves validation for job names, paths, scheduling inputs, and destructive sync options before save.

## Bug Fixes
- **Preview Access Errors**: Fixes preview failures on protected Windows folders by safely skipping inaccessible system directories during enumeration.
- **One-Time Scheduling Validation**: Fixes invalid one-time scheduling by blocking past date/time values and showing clear validation messages.
- **Startup Database Lock Handling**: Fixes startup reliability by retrying database migrations when SQLite is temporarily locked.
- **Multi-Instance Conflict Protection**: Fixes duplicate-instance issues by enforcing single-instance startup behavior.

## Technical Changes
- Migrates persistence to **SQLite + Entity Framework Core migrations** for stronger data consistency and schema evolution.
- Integrates **Quartz.NET persistent scheduling** for manual, one-time, and recurring trigger execution.
- Introduces configurable application and execution log retention to control local log growth.
- Targets **.NET 9** across the desktop and core runtime stack.

## Breaking Changes (if any)
- No user-facing breaking changes in this release.

## Installation
- Download from: https://github.com/Ignyos/Archive/releases/latest
- Run `ArchiveSetup.exe`

## Requirements
- Windows 10/11 (64-bit)
- .NET 9.0 Runtime (included in installer)

## Documentation
- Docs home: https://ignyos.github.io/Archive/
- Getting started: https://ignyos.github.io/Archive/getting-started.html
- Troubleshooting: https://ignyos.github.io/Archive/troubleshooting.html
- Changelog: https://ignyos.github.io/Archive/changelog.html

