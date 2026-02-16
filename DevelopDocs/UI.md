# Archive User Interface Specification

This document details the user interface design for the Archive application.

## Main Window

### Overall Layout

**Title Bar**:
- Application icon + "Archive" name
- Standard Windows controls: Minimize, Maximize/Restore, Close
- **Minimize button behavior**: Standard Windows (minimize to taskbar)
- **Close button behavior**: Minimizes to system tray (does not exit application)

**Menu Bar**:
- **File**
  - New Job (`Ctrl+N`)
  - Settings
  - About

**Toolbar**:
- None (actions accessed via File menu and keyboard shortcuts)
  
**Main Content Area**:
- Job list (DataGrid)

**Status Bar**:
- Version status message:
  - "You're on the most recent version: v#.#.#"
  - "A new version is available: v#.#.#" (hyperlink to downloads)

---

### Job List Display

**Columns** (left to right):
1. **Status** (text only)
   - **Idle**: Not scheduled and not currently running (black text)
   - **Scheduled**: Scheduled and not currently running (black text)
   - **Running**: Currently running (black text)
     - *(Future)* Progress indicator or hover tooltip with percentage
   - **Warning**: Missed execution due to reasons other than manual disable/pause (red text)
   - **Error**: Execution error occurred (red text)
     - Includes: File operation failures (locked files, permission denied), drive unavailability, insufficient disk space
     - Excludes: Configuration issues (caught during job create/edit)

2. **Enabled** (checkbox)
   - User can toggle job enabled/disabled directly in list

3. **Name** (text)
   - Display name of the backup job

4. **Description** (text)
   - User notes about the job

5. **Next Run** (nullable DateTime)
   - Next scheduled execution time
   - Empty for manual-only jobs or disabled jobs

6. **History** (hyperlink)
   - Click to open Job History window

---

### Job List Interactions

**Right-Click Context Menu**:
- **Section 1**
  - Edit Job
  - Delete Job
- **Section 2**
  - Run Now
  - Stop Job
- **Section 3**
  - View History

**Double-Click**:
- Opens Job Create/Edit window

---

## System Tray

**Tray Icon**:
- Always visible when Archive is running
- Shows Archive application icon (same icon regardless of state)

**Tray Icon Interactions**:
- **Left-click**: Opens Archive window (or brings to front if already open)
- **Right-click**: Shows context menu

**Right-Click Context Menu**:
- **Section 1**
  - Open Archive (brings window to front/focus if already open)
- **Section 2**
  - Scheduler Running (checkbox to turn scheduler on/off)
  - Run on Windows Startup (checkbox to enable/disable startup)
- **Section 3**
  - Shut down Archive

---

## About Dialog

**Window Type**: Modal dialog

**Content** (centered, top to bottom):
- **Full Name**: "Archive - Backup Manager"
- **Tag Line**: "TBD"
- **Version**: Current version number (e.g., "v1.0.0")
- **Copyright**: © [current year]
- **Website**: [https://archive.ignyos.com](https://archive.ignyos.com) (hyperlink)
- **Icon Credit**: "Icon credit: Server Rack Vectors by Vecteezy"
  - Link: [https://www.vecteezy.com/free-vector/server-rack](https://www.vecteezy.com/free-vector/server-rack)

**Close**: Standard X button only

---

## Settings Window

**Window Type**: Modal dialog

**Layout**: 
- Each section has a legend (group box)
- Each setting has:
  - Control (checkbox, dropdown, etc.)
  - Label
  - Description text below

**Auto-Save Behavior**:
- Settings automatically saved when changed
- No Save/Cancel buttons needed

**Close**: Standard X button only

**Sections**:

### General
- **(Checkbox) Run Archive when Windows starts**
  - Description: "When enabled, Archive will start automatically in the system tray when you log in to Windows."

### Notifications
- **(Checkbox) Enable notifications**
  - Description: "Show Windows toast notifications for job events."
- **(Checkbox) Notify when job starts**
  - Description: "Show a notification when a scheduled job begins execution. (Only applies if notifications are enabled)"
- **(Checkbox) Notify when job completes**
  - Description: "Show a notification when a job finishes successfully. (Only applies if notifications are enabled)"
- **(Checkbox) Notify when job fails**
  - Description: "Show a notification when a job completes with warnings or errors. (Only applies if notifications are enabled)"
- **(Checkbox) Play notification sound**
  - Description: "Play the system notification sound when toast notifications appear. (Only applies if notifications are enabled)"

**Note**: *(Future) Per-job notification overrides can be configured in the Job Create/Edit window to customize notification behavior for individual jobs.*

### Advanced
- **(Text box + Dropdown) Log retention period**
  - Label: "Keep logs for [__] [Days/Months ▼]"
  - Description: "Execution logs older than this period will be automatically deleted. Enter 0 to keep logs forever."
- **(Checkbox) Enable verbose logging**
  - Description: "Turn on detailed diagnostic logging for troubleshooting. *(Feature design TBD)*"

---

## Future Features

### Progress Window (Real-Time Execution Monitor)

**Window Type**: Non-modal window

**Purpose**: Display live progress and detailed status during job execution

**Layout**:
- Single-column vertical layout
- Updates in real-time via StatusChanged events

**Content**:
- **Current File**: Full path of file being processed
- **Progress Bar**: Visual 0-100% indicator
- **File Counters**: "Copied: X, Updated: Y, Skipped: Z, Failed: W"
- **Data Transferred**: "512 MB / 2.3 GB"
- **Estimated Time Remaining**: Calculated after 10% complete
- **Live Log Stream** (last 100 messages):
  - Scrolling text area
  - Color-coded by level: Info (black), Warning (orange), Error (red)
  - Auto-scrolls to bottom
- **Cancel Button**: 
  - Stops execution immediately
  - Shows confirmation: "Are you sure? Already processed files will remain."
  - Updates job status to 'Cancelled'

**Access Points**:
- Double-click running job in Main Window
- Right-click running job → "View Progress"
- Click "View Progress" button if job already running when "Run Now" clicked

**Notes**: 
- This feature requires implementing per-file progress tracking in the sync engine
- See Requirements.md Functional Requirements → General Behavior → Progress Reporting

---

## Job History Window

**Window Type**: Non-modal (only one can be open at a time)

**Access Points**:
- Main Window: Click "History" hyperlink in job row
- Main Window: Right-click job → "View History"
- Main Window: Double-click job (if not currently running)

**Title Bar**: Standard icon, "Job History - [Job Name]", standard controls

**Info Section**:
- **Job**: [Job name]
- **Source**: [Source path from job]
- **Backup**: [Destination path from job]

**Main List** (execution history):

**Columns** (left to right):
1. **Run Time** (DateTime) - Execution start time
2. **Duration** - Format: `##h ##m ##s` (omit zero segments, e.g., "5m 23s" for sub-hour jobs)
3. **Files Scanned** (int)
4. **Files Copied** (int)
5. **Files Updated** (int)
6. **Files Deleted** (int)
7. **Files Failed** (int)
8. **Details** (hyperlink) - Opens Job Details Window for this execution

**List Behavior**:
- Default sort: Run Time descending (most recent first)
- No custom sorting or filtering in initial version
- Vertical scroll as needed

---

## Job Details Window

**Window Type**: Non-modal (only one can be open at a time)

**Access Points**:
- Job History Window: Click "Details" hyperlink for specific execution

**Title Bar**: Standard icon, "Job Details - [Job Name]", standard controls

**Info Section**:
- **Job**: [Job name]
- **Run Time**: [DateTime] | **Duration**: [##h ##m ##s format] | **Status**: Success / Missed / Run With Errors / Failed
- **Copied**: [int] | **Updated**: [int] | **Deleted**: [int] | **Failed**: [int]

**Collapsible Sections** (four sections, each with vertical & horizontal scroll):

### Copy Operations
- Default: Collapsed
- **Per-File Format**: `[HH:MM:SS] Source\path\file.txt -> Destination\path\file.txt`
- Sort: Start timestamp ascending

### Update Operations
- Default: Collapsed
- **Per-File Format**: `[HH:MM:SS] Source\path\file.txt -> Destination\path\file.txt`
- Sort: Start timestamp ascending

### Delete Operations
- Default: Collapsed
- **Per-File Format**: `[HH:MM:SS] Source\path\file.txt -> Destination\path\file.txt`
- Sort: Start timestamp ascending

### Failed Operations
- Default: Expanded (if any failures exist)
- **Per-File Format**:
  ```
  [HH:MM:SS] Source\path\file.txt -> Destination\path\file.txt
      Error message: [Detailed error text]
  ```
- Sort: Start timestamp ascending

### Skipped Files
- Default: Collapsed
- Only appears if hidden/system files were skipped during scan
- **Per-File Format**: `[HH:MM:SS] Source\path\file.txt (Hidden)` or `(System)`
- Sort: Start timestamp ascending

**Notes**:
- No search, filter, or export in initial version
- Each section scrolls independently

---

## Job Create/Edit Window

**Window Type**: Modal dialog

**Access Points**:
- Main Window: File → New Job (Create mode)
- Main Window: Double-click job (Edit mode)
- Main Window: Right-click job → Edit (Edit mode)

**Title Bar**: Dynamic based on mode
- Create mode: "Create New Job"
- Edit mode: "Edit Job"

**Window Layout** (top to bottom):

### Basic Information Section

**Job Name**:
- Label: "Job Name"
- Control: Single-line text input
- Validation: Required, must be unique

**Description**:
- Label: "Description"
- Control: Multi-line text area (3-4 lines visible)
- Validation: Optional

**Source**:
- Label: "Source Folder"
- Control: Single-line text input + Browse button
- Browse button: Opens folder picker dialog (folder selection only, no single file support)
- Validation: Required, must exist, must be accessible

**Destination**:
- Label: "Destination Folder"
- Control: Single-line text input + Browse button
- Browse button: Opens folder picker dialog (folder selection only, no single file support)
- Validation: Required, must exist, must be accessible, cannot be same as source or subdirectory of source

**Job Enabled**:
- Control: Checkbox "Job Enabled"
- Default: Checked (enabled)

---

### Sync Options Section

**Label**: "Sync Options"

**Include subdirectories**:
- Control: Checkbox "Include subdirectories (recursive)"
- Default: Checked (enabled)
- Tooltip: "When enabled, all files and folders within the source folder will be synchronized"

**Delete orphaned files**:
- Control: Checkbox "Delete files in destination that don't exist in source (destructive)"
- Default: Unchecked (disabled)
- Behavior: When checked, shows confirmation dialog immediately:
  - Title: "Confirm Destructive Operation"
  - Message: "This option will permanently delete files from the destination that are not present in the source. Are you sure?"
  - Buttons: **Yes** / **No** / **Preview**
    - Yes: Keeps checkbox checked, closes dialog
    - No: Unchecks checkbox, closes dialog
    - Preview: Keeps checkbox checked, closes dialog, runs preview operation (see Preview Operations below)

**Skip hidden/system files**:
- Control: Checkbox "Skip hidden/system files"
- Default: Checked (enabled)
- Tooltip/Info Icon: "When enabled, files marked as Hidden or System by Windows will be ignored during synchronization. Skipped files are listed in the preview and job details."

**Verify after copying**:
- Control: Checkbox "Verify files after copying (slower but safer)"
- Default: Unchecked (disabled)
- Tooltip: "When enabled, file hashes are compared after copy operations to ensure data integrity"

---

### Schedule Section

**Label**: "Schedule"

**Trigger Type Selection**:
- Control: Radio buttons (horizontal layout on single line)
- Options: ○ Manual | ○ One-Time | ○ Recurring
- Default: Manual
- Behavior: Selecting a type shows relevant controls below

---

#### Manual Mode

**Description**:
- No automatic schedule - job only runs when manually triggered

**Additional Controls**:
- None (no date/time pickers or schedule configuration)

**Control Button Behavior**:
- **OK button changes to "Run" button**
- Clicking "Run" saves the job and immediately starts execution
- Preview and Cancel buttons remain unchanged

---

#### One-Time Mode

**Description**:
- Job runs once at a specified future date/time

**Controls**:

**Execution Date/Time**:
- Label: "Run on"
- Control: Date/Time picker (combined or separate controls)
- Validation: Must be future date/time (cannot be in the past)

**Preview Text**:
- Display: "This job will run once on [formatted date/time]"
- Format: "Monday, February 14, 2026 at 3:30 PM" (or similar user-friendly format)
- Updates in real-time as user changes date/time

---

#### Recurring Mode

**Description**:
- Job runs on a repeating schedule defined by cron expression

**Schedule Builder**:
- Control: Tab control with three tabs
- Tabs: [Simple] [Advanced Builder] [Cron Expression]

---

##### Simple Tab

**Preset Selector**:
- Control: Dropdown menu
- Options:
  1. **Every Hour** - Runs at the top of every hour (12:00, 1:00, 2:00, etc.)
  2. **Every Day at [time picker]** - Runs once daily at specified time
  3. **Every Week on [day checkboxes] at [time picker]** - Runs on selected days at specified time
  4. **Every Month on [day number] at [time picker]** - Runs monthly on specified day at specified time

**Configuration Controls** (appear based on dropdown selection):

**Every Hour**:
- No additional controls (runs literally every hour on the hour)

**Every Day**:
- Time picker control
- Default: 12:00 AM (midnight)

**Every Week**:
- Day checkboxes (horizontal): ☐ Sun ☐ Mon ☐ Tue ☐ Wed ☐ Thu ☐ Fri ☐ Sat
- Time picker control
- Validation: At least one day must be selected

**Every Month**:
- Day number selector (1-31 or dropdown/spinner)
- Time picker control
- Note: "Jobs scheduled for day 29-31 may not run in all months"

**Behavior**:
- Dropdown selection changes configuration controls displayed
- Generates Quartz cron expression behind the scenes
- Switching away from Simple tab to Advanced/Cron discards Simple configuration (no warning)

---

##### Advanced Builder Tab

**Purpose**: Comprehensive cron expression builder with full Quartz.NET capabilities

**Layout**: Tab control for each time component

**Tabs**: [Minutes] [Hours] [Day] [Month] [Year]
- Note: Seconds are omitted (not needed for backup scheduling)

**Per-Tab Options** (similar to freeformatter.com):

**Every Tab Includes**:
- Radio button or option for "Every [unit]" (e.g., Every minute, Every hour)
- Radio button or option for "Every X [unit]" with number input (e.g., Every 5 minutes)
- Radio button or option for "Specific [unit](s)" with checkboxes or multi-select
- Radio button or option for "Range" with from/to inputs
- Additional options specific to Day/Month (e.g., Last day of month, Weekdays only, etc.)

**Generated Cron Expression**:
- Display: Read-only text box at bottom showing generated cron expression
- Updates in real-time as user makes selections
- Format: Standard Quartz cron (7 fields: seconds minutes hours day month weekday year)

**Synchronization with Cron Expression Tab**:
- Switching from Advanced Builder → Cron Expression: Cron text box populated with generated expression
- Switching from Cron Expression → Advanced Builder: 
  - If cron is valid and parseable, Advanced Builder controls update to reflect it
  - If cron is invalid or too complex, show warning: "Cannot parse cron expression for Advanced Builder. Clearing Advanced Builder state."

---

##### Cron Expression Tab

**Purpose**: Direct cron expression entry for advanced users

**Controls**:

**Cron Expression Input**:
- Label: "Cron Expression"
- Control: Multi-line text box (2-3 lines visible)
- Validation: Real-time validation using Quartz.NET parser
- Invalid expression: Red border + error message below

**Help Section**:
- Tooltip or info icon with link to Quartz cron documentation
- Examples displayed below text box:
  ```
  Examples:
  0 0 12 * * ?        Every day at noon
  0 0/15 * * * ?      Every 15 minutes
  0 0 12 ? * MON-FRI  Every weekday at noon
  0 0 0 1 * ?         First day of every month at midnight
  ```

**Synchronization with Advanced Builder Tab**:
- Switching from Cron Expression → Advanced Builder: Attempts to parse and populate Advanced Builder
- Switching from Advanced Builder → Cron Expression: Populates with generated cron

**Behavior**:
- Switching away from Cron Expression to Simple tab discards cron (no warning)
- Switching between Cron Expression and Advanced Builder maintains synchronization

---

### Control Buttons

**Layout**: Single horizontal row, right-aligned

**Buttons** (left to right):
1. **Preview Operations**
   - Enabled: Only when all validation passes (name, source, destination)
   - Behavior: 
     - Opens modal progress dialog with indeterminate progress bar and Cancel button
     - Performs dry-run scan of source/destination
     - When complete (or cancelled), closes progress dialog and opens Job Details Window showing what *would* happen if job runs
     - Job Create/Edit window remains open
   - Can be clicked multiple times (re-runs preview each time)

2. **OK**
   - Enabled: Only when all validation passes
   - Behavior: Saves job to database, closes window

3. **Cancel**
   - Enabled: Always
   - Behavior: Discards changes, closes window

**Validation Behavior**:
- Real-time validation as user types/changes fields
- Invalid fields highlighted with red border + error message below field
- Preview and OK buttons disabled until all validation passes

**Notes**:
- Window does not auto-save (only saves on OK)
- All default values apply to Create mode only (Edit mode loads existing values)
- Changes to "Delete orphaned files" checkbox show confirmation dialog in both Create and Edit modes
