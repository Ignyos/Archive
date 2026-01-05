namespace Archive.Core;

/// <summary>
/// Represents the result of a synchronization operation.
/// </summary>
public class SyncResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the synchronization was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the number of files copied.
    /// </summary>
    public int FilesCopied { get; set; }

    /// <summary>
    /// Gets or sets the number of files updated.
    /// </summary>
    public int FilesUpdated { get; set; }

    /// <summary>
    /// Gets or sets the number of files deleted.
    /// </summary>
    public int FilesDeleted { get; set; }

    /// <summary>
    /// Gets or sets the number of files skipped.
    /// </summary>
    public int FilesSkipped { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes transferred.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Gets or sets the duration of the synchronization operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets any errors encountered during synchronization.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets any warnings encountered during synchronization.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of planned operations (populated in dry run mode).
    /// </summary>
    public List<SyncOperation> PlannedOperations { get; set; } = new();

    /// <summary>
    /// Gets or sets the reason why the synchronization stopped.
    /// </summary>
    public SyncStoppedReason StoppedReason { get; set; } = SyncStoppedReason.Completed;
}

/// <summary>
/// Defines the reasons why a synchronization operation stopped.
/// </summary>
public enum SyncStoppedReason
{
    /// <summary>
    /// The synchronization completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The synchronization was cancelled via CancellationToken.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The synchronization stopped due to schedule boundary constraints.
    /// </summary>
    ScheduleBoundary,

    /// <summary>
    /// The synchronization stopped due to an error.
    /// </summary>
    Error
}
