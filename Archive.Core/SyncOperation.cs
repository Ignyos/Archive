namespace Archive.Core;

/// <summary>
/// Represents a planned synchronization operation.
/// </summary>
public class SyncOperation
{
    /// <summary>
    /// Gets or sets the type of operation.
    /// </summary>
    public SyncOperationType Type { get; set; }

    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Gets or sets the destination file path.
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets a description of the operation.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Defines the types of synchronization operations.
/// </summary>
public enum SyncOperationType
{
    /// <summary>
    /// Copy a new file to the destination.
    /// </summary>
    Copy,

    /// <summary>
    /// Update an existing file in the destination.
    /// </summary>
    Update,

    /// <summary>
    /// Delete a file from the destination.
    /// </summary>
    Delete,

    /// <summary>
    /// Skip a file (no changes needed).
    /// </summary>
    Skip
}
