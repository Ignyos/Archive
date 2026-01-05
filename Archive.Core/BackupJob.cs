namespace Archive.Core;

/// <summary>
/// Represents a backup job with source, destination, and synchronization options.
/// </summary>
public class BackupJob
{
    /// <summary>
    /// Gets or sets the unique identifier for this backup job.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the name of the backup job.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description of the backup job.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the source directory path to back up from.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the destination directory path to back up to.
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the synchronization options for this job.
    /// </summary>
    public SyncOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the schedule for this job.
    /// </summary>
    public OperationSchedule Schedule { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this job is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show notifications when this job completes.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp of the last successful execution.
    /// </summary>
    public DateTime? LastRunTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the next scheduled execution.
    /// </summary>
    public DateTime? NextRunTime { get; set; }

    /// <summary>
    /// Gets or sets the result of the last execution.
    /// </summary>
    public SyncResult? LastResult { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this job is currently running.
    /// This is a runtime property and is not persisted.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for this job.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
