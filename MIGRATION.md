# SQLite Migration Guide

## Overview

The Archive GUI application has been migrated from JSON-based storage to SQLite database for enhanced logging, history tracking, and reporting capabilities.

## What Changed

### GUI Application (`Archive.GUI`)

**Before:**
- Used `JobStorageService` to store jobs in `%APPDATA%\Archive\jobs.json`
- No history tracking
- No detailed logging per job run

**After:**
- Uses `DatabaseService` to store jobs in `%APPDATA%\Archive\archive.db`
- Full history tracking with `JobHistory` and `JobLogs` tables
- Automatic migration from existing `jobs.json` on first run
- Original JSON file renamed to `jobs.json.migrated` after successful migration

### Console Application (`Archive.Console`)

**No Changes:**
- Still uses JSON configuration files passed via command-line arguments
- Completely independent from GUI storage
- No database dependency

## Database Schema

### Jobs Table
- Stores job configurations (name, source path, destination path, options, schedule, etc.)
- Equivalent to what was stored in JSON format

### JobHistory Table
- Records every job execution
- Stores success/failure status, statistics (files copied/updated/deleted/failed), start/end times
- Foreign key relationship to Jobs table

### JobLogs Table
- Detailed operation logs (file copied, file updated, file deleted, errors)
- Foreign key relationship to JobHistory table

### Settings Table
- Application-wide settings (HasLaunchedBefore, HasPromptedForStartup, DisableAllNotifications)
- Key-value pairs for extensibility

## Migration Process

On first launch after update:

1. GUI checks for `%APPDATA%\Archive\jobs.json`
2. If found, automatically imports all jobs to SQLite database
3. Imports global settings (startup prompts, notification preferences)
4. Renames `jobs.json` to `jobs.json.migrated` (backup)
5. GUI runs normally using SQLite

## Benefits

1. **History Tracking**: View past job executions, success rates, statistics
2. **Detailed Logging**: Every file operation logged per job run
3. **Better Performance**: Indexed queries for large job histories
4. **Data Integrity**: ACID transactions prevent data corruption
5. **Extensibility**: Easy to add new features (reporting, analytics, retry logic)

## Backward Compatibility

- Existing GUI users: Automatic migration on first launch
- Console users: No changes required, continues to use JSON configs
- No data loss: Original JSON file preserved as `.migrated` backup

## Developer Notes

### DatabaseService Methods

- `InitializeDatabase()`: Creates schema if not exists
- `LoadJobsAsync()`: Reads jobs and settings
- `SaveJobsAsync(collection)`: Saves jobs and settings
- `SaveJobHistoryAsync(job, result)`: Records job execution
- `GetJobHistoryAsync(jobId, limit)`: Retrieves historical runs
- `MigrateFromJsonAsync(jsonPath)`: One-time JSON import

### History Integration

The `SchedulerService` now automatically saves job history after each execution:

```csharp
await _databaseService.SaveJobHistoryAsync(job, result);
```

This enables future features like:
- "View History" in JobReportWindow
- Job execution statistics dashboard
- Automatic retry of failed operations
- Email/SMS notifications with historical context

## Future Enhancements

- Add UI to view job history (past executions)
- Statistical reports (success rate, avg duration, bandwidth usage)
- Search and filter logs
- Export logs to CSV/Excel
- Retention policies (auto-delete old logs after N days)
