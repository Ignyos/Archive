# Archive application description

Archive is an application for creating and managing backups of digital resources. A source directory (or file) can be selected and a destination. The destination can be a one to one mirror (files deleted from the source are deleted in the destination) or it can be a place for new files from the source to be copied. Multiple backup jobs can be defined and all can be run immediately, once in the future, or on a complex repeating schedule.

## Archive requirements

### Tech stack and dependencies

- Microsoft WPF for the UI
- .NET 9 (or recent LTS version) for business logic
- Quartz.NET for scheduling
- SQLite for data persistence
- Entity Framework for migrations
- InnoSetup for installers
- Powershell script for automating releases
- GitHub actions for deployment cycle
- AI for code generation
- Test driven development (library TBD)

### Code architecture

#### Primary namespaces: (each has a folder at the root)
- Archive.Console - CLI tool for testing/automation
- Archive.Core - Domain models, sync engine, provider abstractions
- Archive.Infrastructure - Provider implementations, EF Core repos
- Archive.Desktop - WPF UI, ViewModels, services

#### docs
- folder at root for GitHub pages.
- Splash page for high level app description and downloads
- Documentation pages for in depth detailed usage

#### DevelopDocs folder
- folder at root where this document is stored
- Will later include impementation phase documents, etc...

#### .vscode
- folder at root for Run and Debug scripts. Some will be used for Run & Debug and at least one for starting the build

#### publish
- folder for local installer scripts for development

#### .github/workflows
- folder for github actions

#### scripts
- folder for all scripts
- github actions
- building installers with InnoSetup
- pre-GitHub script for generating relase notes

## Functional Requirements

### Sync Modes

**Sync Strategy** (Choose one, configurable per job):

1. **Mirror Mode** (One-way: Source → Destination)
   - Destination exactly matches source
   - Files deleted from source are deleted from destination
   - Files in destination but not in source are removed
   - **Deleted File Handling** (sub-option):
     - Delete immediately from destination
     - Move to recycle bin/archive folder before permanent deletion
   
2. **Incremental Mode** (One-way: Source → Destination)
   - Destination accumulates changes over time
   - Only adds new files and updates existing files
   - Never deletes files from destination

**Overwrite Behavior** (Configurable per job):
- **Always Overwrite** (default): Destination file replaced with source version
- **Keep Both**: Preserve destination file with timestamp suffix (e.g., `file_2026-02-12_143052.txt`)
  - ⚠️ **Not available in Mirror Mode** - conflicts with exact mirroring requirement

**Direction**:
- One-way only: Source → Destination
- *(Future Feature)* Bi-directional sync not currently supported

### File Comparison Strategy

**Comparison Methods** (Configurable per job):
- **Fast Mode**: File size + last modified date comparison
- **Accurate Mode**: Content hash (MD5 or SHA256)

**File Attributes**:
- All attributes are preserved when creating/updating files:
  - Read-only flag
  - Hidden flag
  - System flag
  - Timestamps (created, modified, accessed)
- Attribute changes trigger file updates

**Verification** (Optional, configurable per job):
- After copying, verify file integrity using hash comparison
- Ensures destination file exactly matches source
- Recommended for critical data, optional for performance

### Exclusion Rules

**Pattern Matching** (TBD - needs user-friendly syntax):
- Possible options under consideration:
  - Wildcards: `*.tmp`, `cache/*`
  - Glob patterns: `**/*.log`
  - Regex
  - .gitignore style syntax

**Exclusion Scopes**:
- **Per-Job Exclusions**: Custom patterns for each backup job
- **Global Exclusions**: System-wide skip list (e.g., `thumbs.db`, `desktop.ini`)
  - Presented as suggestions when source/destination are root drives (C:\, D:\)
- **File Size Exclusions**: *(Future Feature)* Skip files larger than threshold

**Special File Handling** (Configurable per job):
- Separate checkboxes for:
  - Include/exclude hidden files
  - Include/exclude system files
- **Ignore Files**: *(Future Feature)* Respect `.gitignore` or `.archiveignore` files in directories

### Error Handling

**File Operation Failures** (Locked files, permission denied):
- **Default Behavior**: Skip file and continue with others
- Log error with full file path for review
- Configurable per job (default: log and continue)

**Job Success/Failure Criteria**:
- Jobs are **not** marked as simply "success" or "failure"
- Completion summary shows:
  - Total files processed
  - Successful operations
  - Failed operations
  - Skipped files
- **Warning Indicator**: Displayed if job completed with any errors
- Detailed log stored for every job regardless of error count

**Network/Drive Unavailability**:
- If source or destination becomes unavailable mid-job:
  - Stop job immediately
  - Log error message with context
  - Record which files were successfully updated before failure
- Applies to all storage types (local, network, removable USB drives)

**Disk Space Management**:
- **Pre-check**: Calculate required space before starting job
- If insufficient space:
  - Log the issue with space requirements vs available space
  - Do not start the job
  - Notify user

### General Behavior

**Dry Run Mode**:
- Preview what would happen without executing operations
- Shows overview of:
  - Files to be added
  - Files to be updated
  - Files to be deleted (if mirror mode)
  - Files to be left in place (if incremental mode)
  - Total data size of operation
- **Save Preview**: Optional - user can choose to save preview results
  - *(Needs review: previews are only reliable at time of check)*

**Symbolic Links / Junctions**:
- Copy the link itself, not the target contents
- Preserves link structure without following

**File Locking**:
- If source file is open/locked:
  - Skip file immediately (no waiting/retry)
  - Log status as failed for that file
  - Continue with remaining files

**Progress Reporting**:
- **Per-File Progress**: *(Future Feature)* Not in initial version
- **Overall Progress**: Display both:
  - Files processed / total files
  - Percentage complete
  - Current operation description

## Domain Model

### Entities

#### BackupJob
**Purpose**: Configuration for a sync/backup operation

**Properties**:
- `Id` (Guid) - Primary key, unique identifier
- `Name` (string, optional) - Display name for the job
- `Description` (string, optional) - User notes about the job
- `SourcePath` (string, required) - Source directory or file path
- `DestinationPath` (string, required) - Destination directory path
- `Enabled` (bool) - Master switch: disabled jobs never run (overrides all other settings)
- `SyncMode` (enum: Mirror, Incremental) - Strategy for handling files
- `ComparisonMethod` (enum: Fast, Accurate) - How to detect file changes
- `DeletedFileHandling` (enum: DeleteImmediately, MoveToRecycleBin) - Mirror mode only
- `OverwriteBehavior` (enum: AlwaysOverwrite, KeepBoth) - Conflict resolution (KeepBoth disabled in Mirror mode)
- `ExclusionPatterns` (navigation property) - Collection of exclusion rules
- `SyncOptionsId` (foreign key) - Reference to SyncOptions configuration
- `TriggerType` (enum: Recurring, OneTime, Manual) - How job is scheduled
  - **Recurring**: Uses CronExpression for repeating schedule
  - **OneTime**: Uses SimpleTriggerTime for single future execution
  - **Manual**: No automatic schedule, user-initiated only
- `CronExpression` (string, optional) - Quartz.NET cron (required if TriggerType=Recurring)
- `SimpleTriggerTime` (DateTime, optional) - Single execution time (required if TriggerType=OneTime)
- `CreatedAt` (DateTime) - When job was created
- `ModifiedAt` (DateTime) - Last configuration change
- `LastRunAt` (DateTime, optional) - Most recent execution timestamp
- `DeletedAt` (DateTime, optional) - Soft delete timestamp (null = active)
- `NotifyOnStart` (bool, optional) - Override global setting for job start notifications (null = use global setting)
- `NotifyOnComplete` (bool, optional) - Override global setting for job completion notifications (null = use global setting)
- `NotifyOnFail` (bool, optional) - Override global setting for job failure notifications (null = use global setting)

**Notes**:
- Minimal valid job: SourcePath + DestinationPath + TriggerType=Manual
- Recurring scheduled job requires: SourcePath + DestinationPath + TriggerType=Recurring + Enabled=true + valid CronExpression
- One-time scheduled job requires: SourcePath + DestinationPath + TriggerType=OneTime + Enabled=true + SimpleTriggerTime (future date/time)
- `CronExpression` and `SimpleTriggerTime` are mutually exclusive based on TriggerType
- `Enabled` is the master switch: when false, job never runs (regardless of TriggerType or schedule)
- Global "Archive Schedule Enabled" setting in Main Window system tray can disable all scheduled jobs system-wide
- Notification override fields (NotifyOnStart, NotifyOnComplete, NotifyOnFail) are nullable:
  - null = use global notification settings from AppSettings
  - true/false = override global setting for this specific job

---

#### SyncOptions
**Purpose**: Reusable sync behavior configuration (value object with persistence)

**Properties**:
- `Id` (Guid) - Primary key
- `Recursive` (bool) - Include subdirectories (default: true)
- `DeleteOrphaned` (bool) - Remove files not in source (default: false)
- `VerifyAfterCopy` (bool) - Hash verification after write (default: false)
- `SkipHiddenAndSystem` (bool) - Skip files marked Hidden or System by Windows (default: true)

**Relationships**:
- One SyncOptions → Many BackupJobs (shared configuration)

**Notes**:
- Initial implementation: Each BackupJob automatically creates its own SyncOptions (effectively 1:1)
- Future enhancement: UI for creating/managing/sharing option presets across jobs
- Empty directories are always copied (no separate setting)
- File timestamps and attributes are always preserved (no separate setting)

---

#### ExclusionPattern
**Purpose**: Reusable file/folder exclusion rules

**Properties**:
- `Id` (Guid) - Primary key
- `Name` (string) - Display name (e.g., "Standard System Files")
- `Pattern` (string) - Exclusion syntax (TBD: wildcards/glob/regex)
- `IsGlobal` (bool) - System-wide vs per-job
- `IsSystemSuggestion` (bool) - Suggested when syncing root drives

**Relationships**:
- Many-to-Many with BackupJob (a job can have multiple patterns, a pattern can be used by multiple jobs)

---

#### JobExecution
**Purpose**: Record of a single job run

**Properties**:
- `Id` (Guid) - Primary key
- `JobId` (Guid, foreign key) - Parent BackupJob
- `Status` (enum: Validating, Running, Completed, CompletedWithWarnings, Failed, Cancelled)
  - **Validating**: Pre-run phase checking directory availability and space requirements
  - **Running**: Active file operations
  - **Completed**: Finished with no errors
  - **CompletedWithWarnings**: Finished but some files skipped/failed
  - **Failed**: Could not complete (e.g., drive unavailable, insufficient space)
  - **Cancelled**: User-initiated stop
- `StartTime` (DateTime) - Execution start
- `EndTime` (DateTime, optional) - Execution end
- `Duration` (TimeSpan, calculated) - EndTime - StartTime
- `FilesScanned` (int) - Total files evaluated
- `FilesCopied` (int) - New files written
- `FilesUpdated` (int) - Existing files overwritten
- `FilesDeleted` (int) - Files removed (Mirror mode)
- `FilesSkipped` (int) - Files not processed (locked, excluded, no change, etc.)
- `FilesFailed` (int) - Operation errors
- `BytesTransferred` (long) - Total data written
- `ErrorCount` (int) - Critical errors
- `WarningCount` (int) - Non-critical issues

**Relationships**:
- Many JobExecutions → One BackupJob
- One JobExecution → Many ExecutionLogs

---

#### ExecutionLog
**Purpose**: Detailed error/warning messages for a job execution

**Properties**:
- `Id` (Guid) - Primary key
- `JobExecutionId` (Guid, foreign key) - Parent execution
- `Timestamp` (DateTime) - When logged
- `Level` (enum: Info, Warning, Error) - Severity
- `Message` (string) - Description
- `FilePath` (string, optional) - Affected file if applicable
- `OperationType` (enum, optional: Copy, Update, Delete, Skip) - What was attempted
- `ExceptionDetails` (string, optional) - Stack trace for errors

**Relationships**:
- Many ExecutionLogs → One JobExecution

**Notes**:
- Not every file operation is logged - only summaries, errors, and warnings
- Info logs for key events (job started, validation passed, etc.)
- Warning logs for skipped files (locked, permission denied)
- Error logs for failures (drive unavailable, hash mismatch, etc.)

---

#### AppSettings
**Purpose**: Global application configuration

**Structure**: Key-value pairs (string keys, string values)

**Settings**:
- `GlobalExclusionPatterns` (JSON array) - System-wide file skip list
- `NotificationPreferences` (JSON object) - Toast notification settings
- `LogRetentionDays` (int) - *(Future)* Auto-cleanup policy (currently: keep forever)
- `MaxConcurrentJobs` (int) - Simultaneous job execution limit
- `ArchiveScheduleEnabled` (bool) - Master switch for all scheduled jobs

---

### Relationships

```
BackupJob (1) ─────< (M) JobExecution (1) ─────< (M) ExecutionLog
    │
    │ (1)
    ├─────< (M) BackupJob_ExclusionPattern ───> (M) ExclusionPattern
    │
    └─────> (1) SyncOptions

AppSettings (singleton key-value store)
```

**Key Relationships**:
1. **BackupJob → JobExecution**: One-to-many (job history)
2. **JobExecution → ExecutionLog**: One-to-many (detailed logs)
3. **BackupJob ↔ ExclusionPattern**: Many-to-many (shared exclusion rules)
4. **BackupJob → SyncOptions**: Many-to-one (reusable option sets)

**Cascade Delete Behavior**:
- Deleting BackupJob → Delete all JobExecutions + ExecutionLogs
- User confirmation required + option to export data before deletion
- Deleting SyncOptions → Set BackupJob.SyncOptionsId to null (or prevent if in use)
- Deleting ExclusionPattern → Remove associations, keep jobs intact

---

### Query Patterns

**Common Queries**:
1. Get active jobs: `WHERE DeletedAt IS NULL`
2. Get last N executions for a job (ordered by StartTime DESC)
3. Get all jobs with warnings/errors in last X days
4. Get jobs that haven't run in X days (LastRunAt check)
5. Calculate total bytes transferred per job over time (aggregate BytesTransferred)
6. Get execution details with all logs for history viewer

**UI Scenarios**:
- **Job List**: Show all active BackupJobs (`WHERE DeletedAt IS NULL`) with LastRunAt and last execution status
- **Job History Window**: Show JobExecutions for selected job with status indicators
- **Execution Details**: Show full statistics + all ExecutionLogs (errors/warnings highlighted)
- **Global History**: Show all executions across all jobs (filterable by status, date range)

## Scheduling

### Quartz.NET Integration

**Storage Strategy**: Quartz.NET Persistent Store (SQLite)
- Quartz manages its own tables (qrtz_jobs, qrtz_triggers, qrtz_calendars, etc.)
- BackupJob table stores job configuration (paths, options, etc.)
- Quartz JobDataMap contains only JobId reference
- Job execution loads full config from BackupJob table (always uses latest settings)

**Service Architecture**:
- Archive runs as Windows background service (not just when UI open)
- Scheduler starts on service startup, loads all enabled jobs
- UI communicates with service to add/modify/remove jobs at runtime
- Service persists through system restarts (Quartz persistent store survives crashes)

---

### Trigger Types

**Supported Scheduling Options**:

1. **Recurring Schedule (CronTrigger)**
   - Use Quartz.NET cron expressions
   - Examples:
     - `0 0 2 * * ?` - Daily at 2:00 AM
     - `0 0 2 ? * MON-FRI` - Weekdays at 2:00 AM
     - `0 0 */6 * * ?` - Every 6 hours
   - Full flexibility for complex schedules

2. **One-Time Schedule (SimpleTrigger)**
   - Run once at specific future date/time
   - Example: "Run this backup once tomorrow at 3:00 PM"
   - Job configuration remains in database but trigger is removed after execution

3. **Manual Execution**
   - "Run Now" button in UI
   - Triggers immediate execution regardless of schedule
   - Does not affect next scheduled run

**UI Scheduling Modes**:
- **Simple Mode** (default): Visual helper generates cron expression
  - Dropdowns for: Daily/Weekly/Monthly
  - Time picker for execution time
  - Day of week/month selectors
  - Live preview of generated cron expression
  - "Next 5 run times" preview
  
- **Advanced Mode**: Direct cron expression entry
  - For users who understand cron syntax
  - Validation and error messages for invalid expressions
  - Same "Next 5 run times" preview
  - Can input valid cron and switch to _Simple Mode_ to see UI for editing

- **One-Time Mode**: Date/time picker for single execution
  - Force future date/time

**User Preference**:
- Setting to choose default mode (Simple/Advanced/One-Time) for new jobs
- Per-job override available

---

### Misfire Handling

**Misfire Scenario**: Scheduled job didn't run at expected time (app stopped, system sleeping, etc.)

**Misfire Threshold**: 60 seconds
- Job is considered "misfired" if execution starts >60 seconds after scheduled time
- Typical for overnight backups where exact timing isn't critical

**Misfire Strategy**: `DoNothing` (default for all jobs)
- Skip missed executions entirely
- Resume normal schedule at next scheduled time
- Prevents "catch-up storms" after extended downtime

**Example**:
```
Job scheduled: Daily at 2:00 AM
System off: Tuesday-Thursday
System starts: Friday 9:00 AM

Result: No catch-up runs, next execution is Saturday 2:00 AM
```

**Misfire Notification**:
- Log missed executions for user review
- Optional notification/alert when jobs are missed (Window's toast/alert)
- UI indicator showing "X missed runs in last 30 days" (In app)

---

### Concurrency Rules

**Job-Level Concurrency**:
- **Restriction**: Same job cannot run multiple instances concurrently
- Implementation: `[DisallowConcurrentExecution]` attribute on job class
- Reason: Prevents file conflicts and inconsistent state
- Behavior: If Job A is still running at next scheduled time, the new trigger is misfired (skipped) and logged

**Global Concurrency**:
- **Initial Implementation**: `MaxConcurrentJobs = 1` (sequential execution only)
- **Reason**: Path overlap conflicts
  - Example: Job A (C: → D:) and Job B (D:/folder → E:/folder) share destination
  - Concurrent execution could cause file locking issues
  - *(Future Enhancement)* Automatic Path conflict detection to enable safe concurrent execution at time of creating/editing jobs

**Queue Behavior** (when MaxConcurrentJobs reached):
- Currently N/A (MaxConcurrentJobs = 1 means sequential queue)
- *(Future)* When concurrency > 1:
  - Treat as misfire (skip based on DoNothing strategy)
  - Alternative 1: Configurable per-job (queue vs skip)
  - Alternative 2: Configurable per-job, set priority. Highest priority takes precedence

**Quartz Thread Pool**:
- Configuration: `MaxConcurrency = 10` (thread pool size)
- Actual concurrent jobs limited by `MaxConcurrentJobs` setting (currently 1)
- Provides headroom for future concurrent execution

---

### Job Persistence & Lifecycle

**Job Registration**:
- On service startup: Load all BackupJob entities where `DeletedAt IS NULL AND Enabled = true`
- For each enabled job with `ScheduleEnabled = true`:
  - Create Quartz JobDetail with JobId in JobDataMap
  - Create appropriate Trigger (Cron or Simple)
  - Register with Quartz scheduler

**Runtime Job Management**:
- **Add Job**: Insert BackupJob → Register with Quartz (if enabled + scheduled)
- **Modify Job**: Update BackupJob → Reschedule in Quartz (trigger replacement)
- **Delete Job**: Remove from Quartz → Set `DeletedAt = DateTime.UtcNow` (soft delete) → Keep all related data
- **Enable/Disable Job**: Toggle Quartz trigger paused state
- **Enable/Disable Schedule**: Add/remove Quartz trigger, keep job definition

**Global Schedule Toggle**:
- AppSettings: `ArchiveScheduleEnabled` (bool)
- When false: Pause all Quartz triggers (jobs can still be run manually)
- When true: Resume all triggers for enabled jobs. Missed jobs are logged, with note that they were skipped due to pause.

**Job Execution Flow**:
1. Quartz fires trigger at scheduled time
2. Job's Execute() method called with JobDataMap containing JobId
3. Load full BackupJob configuration from database (ensures latest settings)
4. Perform sync operation via SyncEngine
5. Save JobExecution record with statistics
6. Save ExecutionLog entries for errors/warnings
7. Update BackupJob.LastRunAt timestamp

---

### Monitoring & Observability

**Execution Tracking**:
- Quartz tracks: Currently executing jobs, next fire times, trigger states
- Application queries Quartz for real-time status (not stored in BackupJob)
- UI refreshes state via: `IScheduler.GetCurrentlyExecutingJobs()`

**Job History**:
- Stored in custom JobExecution/ExecutionLog tables (not Quartz tables)
- Quartz tables are ephemeral (scheduling state)
- Application tables are permanent (historical record)

**Key Queries**:
- Is job currently running? → Query Quartz scheduler
- When is next run? → `ITrigger.GetNextFireTimeUtc()`
- What happened last time? → Query JobExecution table
- How many errors? → Query ExecutionLog table

---

### Calendar Exclusions

**Current Implementation**: None

**Future Consideration**:
- Quartz Calendar feature for excluding dates (holidays, maintenance windows)
- Architecture should support adding calendars without schema changes
- Example use case: "Don't run backups on Christmas or New Year's Day"
- Design impact: Minimal - calendars are Quartz-managed, no BackupJob schema changes needed
- Skipped backups are logged with note about why

---

### Listeners & Event Handling

**Event Strategy**: Static events in job class (simple, current approach works)

**Events Fired**:
- `JobStarted` - When Execute() begins
- `JobCompleted` - When Execute() finishes (success, error, or cancelled)
- `StatusChanged` - Progress updates during execution

**Subscribers**:
- UI (if open): Live progress updates, status bar changes
- Notification service: (optional/configurable) Toast notifications for completed jobs
- Logging service: Structured log output

**Rationale**:
- Static events simpler than Quartz IJobListener/ITriggerListener
- Supports ETA/progress reporting (via StatusChanged events)
- Future ETA calculation: Track file processing rate in StatusChanged, project completion time
- Can migrate to Quartz listeners later if multi-subscriber scenarios emerge

---

### Configuration Summary

**Quartz Settings** (stored in appsettings.json or AppSettings table):
```json
{
  "Quartz": {
    "UseInMemoryStore": false,
    "UseSQLite": true,
    "ConnectionString": "Data Source=archive.db",
    "MaxConcurrency": 10,
    "MisfireThreshold": "00:01:00"
  },
  "Archive": {
    "MaxConcurrentJobs": 1,
    "ArchiveScheduleEnabled": true,
    "DefaultSchedulingMode": "Simple"
  }
}
```

**Per-Job Configuration** (stored in BackupJob table):
- `CronExpression` or `SimpleTriggerTime` (nullable, mutually exclusive)
- `ScheduleEnabled` (bool)
- `Enabled` (bool) - Master switch

## Data Model

### Database Schema

**Database Technology**: SQLite with Entity Framework Core 9.0

**Schema Organization**:
- SQLite does not support schemas like SQL Server (no `[schema].[table]` notation)
- **Archive Tables**: Application tables with no prefix (BackupJobs, JobExecutions, etc.)
- **Quartz Tables**: Managed by Quartz.NET with `qrtz_` prefix (qrtz_jobs, qrtz_triggers, etc.)
- Single database file: `archive.db` in AppData
- **Note**: Separation achieved via table naming conventions, not database schemas

---

### Archive Tables (EF Core Entities)

#### BackupJob Table
```sql
CREATE TABLE BackupJobs (
    Id TEXT PRIMARY KEY,
    Name TEXT,
    Description TEXT,
    SourcePath TEXT NOT NULL,
    DestinationPath TEXT NOT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    SyncMode TEXT NOT NULL,  -- 'Mirror' or 'Incremental'
    ComparisonMethod TEXT NOT NULL,  -- 'Fast' or 'Accurate'
    DeletedFileHandling TEXT,  -- 'DeleteImmediately' or 'MoveToRecycleBin'
    OverwriteBehavior TEXT NOT NULL,  -- 'AlwaysOverwrite' or 'KeepBoth'
    SyncOptionsId TEXT,
    TriggerType TEXT NOT NULL,  -- 'Recurring', 'OneTime', 'Manual'
    CronExpression TEXT,
    SimpleTriggerTime TEXT,  -- ISO 8601 DateTime
    ScheduleEnabled INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    ModifiedAt TEXT NOT NULL,
    LastRunAt TEXT,
    DeletedAt TEXT,  -- Soft delete: null = active, non-null = deleted
    NotifyOnStart INTEGER,  -- null = use global, 0 = disabled, 1 = enabled
    NotifyOnComplete INTEGER,  -- null = use global, 0 = disabled, 1 = enabled
    NotifyOnFail INTEGER,  -- null = use global, 0 = disabled, 1 = enabled
    
    FOREIGN KEY (SyncOptionsId) REFERENCES SyncOptions(Id) ON DELETE SET NULL
);

CREATE INDEX IX_BackupJobs_Enabled ON BackupJobs(Enabled);
CREATE INDEX IX_BackupJobs_ScheduleEnabled ON BackupJobs(ScheduleEnabled);
CREATE INDEX IX_BackupJobs_LastRunAt ON BackupJobs(LastRunAt);
CREATE INDEX IX_BackupJobs_TriggerType ON BackupJobs(TriggerType);
CREATE INDEX IX_BackupJobs_DeletedAt ON BackupJobs(DeletedAt);  -- For filtering active jobs
```

#### SyncOptions Table
```sql
CREATE TABLE SyncOptions (
    Id TEXT PRIMARY KEY,
    Recursive INTEGER NOT NULL DEFAULT 1,
    DeleteOrphaned INTEGER NOT NULL DEFAULT 0,
    VerifyAfterCopy INTEGER NOT NULL DEFAULT 0,
    IncludeHidden INTEGER NOT NULL DEFAULT 0,
    IncludeSystem INTEGER NOT NULL DEFAULT 0
);
```

#### ExclusionPattern Table
```sql
CREATE TABLE ExclusionPatterns (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Pattern TEXT NOT NULL,
    IsGlobal INTEGER NOT NULL DEFAULT 0,
    IsSystemSuggestion INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IX_ExclusionPatterns_IsGlobal ON ExclusionPatterns(IsGlobal);
```

#### BackupJob_ExclusionPattern Table (Many-to-Many)
```sql
CREATE TABLE BackupJob_ExclusionPattern (
    BackupJobId TEXT NOT NULL,
    ExclusionPatternId TEXT NOT NULL,
    
    PRIMARY KEY (BackupJobId, ExclusionPatternId),
    FOREIGN KEY (BackupJobId) REFERENCES BackupJobs(Id) ON DELETE CASCADE,
    FOREIGN KEY (ExclusionPatternId) REFERENCES ExclusionPatterns(Id) ON DELETE CASCADE
);
```

#### JobExecution Table
```sql
CREATE TABLE JobExecutions (
    Id TEXT PRIMARY KEY,
    JobId TEXT NOT NULL,
    Status TEXT NOT NULL,  -- 'Validating', 'Running', 'Completed', 'CompletedWithWarnings', 'Failed', 'Cancelled'
    StartTime TEXT NOT NULL,
    EndTime TEXT,
    Duration TEXT,  -- TimeSpan as string
    FilesScanned INTEGER NOT NULL DEFAULT 0,
    FilesCopied INTEGER NOT NULL DEFAULT 0,
    FilesUpdated INTEGER NOT NULL DEFAULT 0,
    FilesDeleted INTEGER NOT NULL DEFAULT 0,
    FilesSkipped INTEGER NOT NULL DEFAULT 0,
    FilesFailed INTEGER NOT NULL DEFAULT 0,
    BytesTransferred INTEGER NOT NULL DEFAULT 0,
    ErrorCount INTEGER NOT NULL DEFAULT 0,
    WarningCount INTEGER NOT NULL DEFAULT 0,
    
    FOREIGN KEY (JobId) REFERENCES BackupJobs(Id) ON DELETE CASCADE
);

CREATE INDEX IX_JobExecutions_JobId ON JobExecutions(JobId);
CREATE INDEX IX_JobExecutions_StartTime ON JobExecutions(StartTime DESC);
CREATE INDEX IX_JobExecutions_Status ON JobExecutions(Status);
CREATE INDEX IX_JobExecutions_JobId_StartTime ON JobExecutions(JobId, StartTime DESC);
```

#### ExecutionLog Table
```sql
CREATE TABLE ExecutionLogs (
    Id TEXT PRIMARY KEY,
    JobExecutionId TEXT NOT NULL,
    Timestamp TEXT NOT NULL,
    Level TEXT NOT NULL,  -- 'Info', 'Warning', 'Error'
    Message TEXT NOT NULL,
    FilePath TEXT,
    OperationType TEXT,  -- 'Copy', 'Update', 'Delete', 'Skip'
    ExceptionDetails TEXT,
    
    FOREIGN KEY (JobExecutionId) REFERENCES JobExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ExecutionLogs_JobExecutionId ON ExecutionLogs(JobExecutionId);
CREATE INDEX IX_ExecutionLogs_Level ON ExecutionLogs(Level);
CREATE INDEX IX_ExecutionLogs_Timestamp ON ExecutionLogs(Timestamp);
```

#### JobAuditLog Table (Change History)
```sql
CREATE TABLE JobAuditLogs (
    Id TEXT PRIMARY KEY,
    JobId TEXT NOT NULL,  -- FK to BackupJobs (but keep even if job deleted)
    JobName TEXT NOT NULL,  -- Snapshot of name at time of change
    Timestamp TEXT NOT NULL,
    ChangeType TEXT NOT NULL,  -- 'Created', 'Modified', 'Deleted', 'Enabled', 'Disabled', 'Executed'
    BeforeSnapshot TEXT,  -- JSON of job state before change
    AfterSnapshot TEXT,  -- JSON of job state after change
    ChangedFields TEXT,  -- JSON array of field names that changed
    Reason TEXT,  -- Optional user-provided reason
    UserId TEXT,  -- *(Future)* Nullable for multi-user scenarios
    
    FOREIGN KEY (JobId) REFERENCES BackupJobs(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_JobAuditLogs_JobId ON JobAuditLogs(JobId);
CREATE INDEX IX_JobAuditLogs_Timestamp ON JobAuditLogs(Timestamp DESC);
CREATE INDEX IX_JobAuditLogs_ChangeType ON JobAuditLogs(ChangeType);
```

#### AppSettings Table (Key-Value Store)
```sql
CREATE TABLE AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

---

### Quartz Tables

Quartz.NET creates its own tables with `qrtz_` prefix:
- `qrtz_job_details` - Job definitions
- `qrtz_triggers` - Trigger configurations
- `qrtz_cron_triggers` - Cron-specific trigger data
- `qrtz_simple_triggers` - Simple trigger data
- `qrtz_fired_triggers` - Currently executing triggers
- `qrtz_paused_trigger_grps` - Paused trigger groups
- `qrtz_scheduler_state` - Scheduler instance state
- `qrtz_locks` - Clustering locks (if using clustering)
- `qrtz_calendars` - Calendar exclusions (if using calendars)

**Note**: Quartz tables managed automatically by Quartz.NET, not by Archive EF migrations.

---

### Entity Relationships Diagram

```
┌─────────────────┐
│   BackupJob     │
└────────┬────────┘
         │ 1:M
         ▼
┌─────────────────┐         ┌─────────────────┐
│  JobExecution   │ ◄─────  │ JobAuditLog     │
└────────┬────────┘         └─────────────────┘
         │ 1:M               (Tracks all job changes)
         ▼
┌─────────────────┐
│  ExecutionLog   │
└─────────────────┘

┌─────────────────┐
│   BackupJob     │
└────────┬────────┘
         │ M:1
         ▼
┌─────────────────┐
│   SyncOptions   │
└─────────────────┘

┌─────────────────┐         ┌─────────────────┐
│   BackupJob     │ M:M     │ ExclusionPattern│
└────────┬────────┘ ◄─────► └─────────────────┘
         (BackupJob_ExclusionPattern join table)

┌─────────────────┐
│   AppSettings   │  (Singleton key-value store)
└─────────────────┘
```

---

### Migration Strategy

**Approach**: Entity Framework Core Migrations (Code-First)

**Initial Setup**:
1. Create initial migration: `dotnet ef migrations add InitialCreate`
2. Apply migration: `dotnet ef database update`
3. EF creates `__EFMigrationsHistory` table tracking applied migrations

**Version Upgrade Flow**:
```
1. User installs new Archive version
2. Service starts, EF checks __EFMigrationsHistory
3. Pending migrations detected:
   a. Display UI prompt: "Database upgrade required. Backup recommended before proceeding."
   b. Offer "Backup Now" button (copies archive.db to Backups folder with timestamp)
   c. Show migration details (version X → Y, breaking changes: Yes/No)
   d. Require user confirmation to proceed ("Upgrade" / "Cancel")
4. User confirms → Apply migrations automatically
5. Log migration results (success/failure, time taken)
6. Service continues normal operation
```

**Backup Prompt Behavior**:
- Non-dismissible until user chooses action
- Default backup location: `%AppData%\Archive\Backups\archive_v{OldVersion}_{Timestamp}.db`
- If backup fails, offer "Continue Anyway" option (not recommended)
- If user cancels upgrade, service exits (prevents running on incompatible schema)

**Migration Types**:

**1. Additive Changes** (Non-Breaking):
- Adding new table
- Adding nullable column
- Adding index
- Example:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>("NewFeatureFlag", "BackupJobs", nullable: true);
    migrationBuilder.CreateIndex("IX_BackupJobs_NewFeatureFlag", "BackupJobs", "NewFeatureFlag");
}
```

**2. Breaking Changes** (Requires Data Migration):
- Renaming column
- Changing column type
- Making nullable column non-nullable
- Splitting/merging columns
- Example (TriggerType introduction):
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Step 1: Add new column (nullable initially)
    migrationBuilder.AddColumn<string>("TriggerType", "BackupJobs", nullable: true);
    
    // Step 2: Data migration - populate based on existing data
    migrationBuilder.Sql(@"
        UPDATE BackupJobs 
        SET TriggerType = CASE
            WHEN CronExpression IS NOT NULL THEN 'Recurring'
            WHEN SimpleTriggerTime IS NOT NULL THEN 'OneTime'
            ELSE 'Manual'
        END
    ");
    
    // Step 3: Make non-nullable after data populated
    migrationBuilder.AlterColumn<string>("TriggerType", "BackupJobs", nullable: false);
    
    // Step 4: Add constraint
    migrationBuilder.Sql(@"
        CREATE TRIGGER validate_trigger_type
        BEFORE INSERT ON BackupJobs
        WHEN NEW.TriggerType = 'Recurring' AND NEW.CronExpression IS NULL
        BEGIN
            SELECT RAISE(ABORT, 'CronExpression required when TriggerType is Recurring');
        END
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP TRIGGER validate_trigger_type");
    migrationBuilder.DropColumn("TriggerType", "BackupJobs");
}
```

**3. Destructive Changes** (Data Loss):
- Dropping table
- Dropping column
- Approach: Warn user, offer data export, require confirmation
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Create backup table first
    migrationBuilder.Sql(@"
        CREATE TABLE BackupJobs_Backup_v2 AS 
        SELECT * FROM BackupJobs
    ");
    
    // Log to AppSettings that backup exists
    migrationBuilder.InsertData("AppSettings", 
        new[] { "Key", "Value" },
        new object[] { "BackupJobs_v2_Backup", DateTime.UtcNow.ToString("o") }
    );
    
    // Perform destructive change
    migrationBuilder.DropColumn("ObsoleteField", "BackupJobs");
}
```

**Version Compatibility Matrix**:
| Archive Version | Min DB Schema | Breaking Changes |
|-----------------|---------------|------------------|
| 1.0.0           | 1             | N/A (initial)    |
| 1.1.0           | 1             | No (additive)    |
| 2.0.0           | 2             | Yes (TriggerType)|
| 2.1.0           | 2             | No (new indexes) |

**Rollback Strategy**:
- EF supports `dotnet ef database update <PreviousMigration>`
- Breaking changes: Down() method must restore data correctly
- Critical: Test migrations on copy of production database first
- Emergency: Keep database backups before major version upgrades

**Quartz Schema Updates**:
- Quartz manages own schema via its initialization
- If Quartz version upgraded, it auto-migrates its tables
- Archive doesn't need to manage Quartz schema changes

---

### Data Integrity Rules

**Constraints**:
1. `BackupJob.SourcePath` ≠ `BackupJob.DestinationPath` (application-level validation)
2. If `TriggerType = 'Recurring'` → `CronExpression` required, `SimpleTriggerTime` null
3. If `TriggerType = 'OneTime'` → `SimpleTriggerTime` required, `CronExpression` null
4. If `TriggerType = 'Manual'` → Both `CronExpression` and `SimpleTriggerTime` null
5. `SyncMode = 'Mirror'` AND `OverwriteBehavior = 'KeepBoth'` → Invalid (application-level)
6. `SimpleTriggerTime` must be future date/time (application-level validation)

**Soft Delete Strategy**:
- "Delete" BackupJob → Set `DeletedAt = DateTime.UtcNow` (soft delete)
- Soft-deleted jobs excluded from UI queries: `WHERE DeletedAt IS NULL`
- Related data preserved:
  - JobExecutions remain accessible (historical record)
  - ExecutionLogs remain accessible (audit trail)
  - JobAuditLogs remain accessible (change history)
  - BackupJob_ExclusionPattern entries remain (for restore scenarios)
- *(Future)* Permanent deletion UI option with confirmation + data export
- *(Future)* "Restore Deleted Job" feature

**Hard Delete Behavior** (if implemented in future):
- Hard delete BackupJob → Cascade delete JobExecutions → Cascade delete ExecutionLogs
- Hard delete BackupJob → Cascade delete BackupJob_ExclusionPattern entries
- Hard delete BackupJob → DO NOT cascade delete JobAuditLogs (historical record)
- Delete SyncOptions → Set BackupJob.SyncOptionsId to NULL (or prevent if in use)
- Delete ExclusionPattern → Cascade delete BackupJob_ExclusionPattern entries

**Foreign Key Behavior**:
```csharp
// In DbContext OnModelCreating:
modelBuilder.Entity<BackupJob>()
    .HasMany(j => j.Executions)
    .WithOne(e => e.Job)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<JobExecution>()
    .HasMany(e => e.Logs)
    .WithOne(l => l.Execution)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<JobAuditLog>()
    .HasOne<BackupJob>()
    .WithMany()
    .HasForeignKey(a => a.JobId)
    .OnDelete(DeleteBehavior.NoAction);  // Keep audit even if job deleted
```

---

### Audit Log Details

**JobAuditLog Use Cases**:

**1. Job Configuration Changes**:
```json
{
  "ChangeType": "Modified",
  "Timestamp": "2026-02-13T14:30:00Z",
  "ChangedFields": ["CronExpression", "ScheduleEnabled"],
  "BeforeSnapshot": {
    "CronExpression": "0 0 2 * * ?",
    "ScheduleEnabled": true
  },
  "AfterSnapshot": {
    "CronExpression": "0 0 3 * * ?",
    "ScheduleEnabled": true
  },
  "Reason": "Changed backup time to 3 AM to avoid peak hours"
}
```

**2. Job Lifecycle Events**:
```json
{
  "ChangeType": "Disabled",
  "Timestamp": "2026-02-13T09:15:00Z",
  "Reason": "Temporarily disabled for system maintenance"
}
```

**3. Execution Tracking** (Summary Only):
```json
{
  "ChangeType": "Executed",
  "Timestamp": "2026-02-13T02:00:05Z",
  "AfterSnapshot": {
    "LastRunAt": "2026-02-13T02:00:00Z",
    "Status": "CompletedWithWarnings"
  }
}
```

**UI Features**:
- **Job History Viewer**: Timeline of all changes to a specific job
- **Diff View**: Side-by-side comparison of before/after states
- **Filter by ChangeType**: Show only modifications, executions, etc.
- **Search by Reason**: Find changes with specific keywords
- *(Future)* Export audit logs to CSV/JSON

---

### Performance Considerations

**Indexes Strategy**:
- Composite index on `(JobId, StartTime DESC)` for job history queries
- Index on `StartTime DESC` for global recent executions
- Index on `Enabled` and `ScheduleEnabled` for scheduler loading
- Index on `Level` in ExecutionLogs for filtering errors/warnings

**Query Optimization**:
- Use `.AsNoTracking()` for read-only queries (history viewing)
- Lazy loading disabled (explicit `.Include()` required)
- Pagination for history lists (50-100 records per page)
- Archive old ExecutionLogs to separate table if >100,000 rows

**Database Size Management**:
- Monitor database file size
- Provide "Compact Database" UI option (SQLite VACUUM)
- *(Future)* Auto-archive old execution logs >1 year

---

### Backup & Export

**Database Backup**:
- SQLite = single file copy
- Provide "Backup Database" UI option
- Suggested backup location: `%AppData%\Archive\Backups\archive_YYYYMMDD_HHMMSS.db`
- *(Future)* Automated periodic backups

**Data Export**:
- Export job configurations to JSON
- Export execution history to CSV
- Export audit logs to JSON
- Use cases: Migrate to new machine, compliance reporting

**Import**:
- Import job configurations from JSON
- Validate before import (no ID conflicts)
- Merge with existing jobs or replace