using System.Text.Json.Serialization;

namespace Archive.Core;

/// <summary>
/// Configuration options for synchronization operations.
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to perform a recursive sync of subdirectories.
    /// </summary>
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to delete files in destination that don't exist in source.
    /// </summary>
    public bool DeleteOrphaned { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to overwrite existing files.
    /// </summary>
    public bool Overwrite { get; set; } = true;

    /// <summary>
    /// Gets or sets file patterns to exclude from synchronization.
    /// By default, includes common system folders and files.
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new()
    {
        "$RECYCLE.BIN",
        "System Volume Information",
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "DumpStack.log.tmp",
        "Thumbs.db",
        ".DS_Store"
    };

    /// <summary>
    /// Gets or sets a value indicating whether to verify files after copying.
    /// </summary>
    public bool VerifyAfterCopy { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to preserve file timestamps.
    /// </summary>
    public bool PreserveTimestamps { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to perform a dry run (preview mode).
    /// When true, no actual changes are made, but planned operations are recorded.
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Gets or sets a callback function to check if the operation should continue.
    /// This can be used to enforce time-based restrictions or other dynamic constraints.
    /// If the function returns false, the operation will stop gracefully.
    /// </summary>
    [JsonIgnore]
    public Func<bool>? ShouldContinue { get; set; }

    /// <summary>
    /// Gets or sets a callback to log individual sync operations as they occur.
    /// Called for each file operation (copy, update, delete, skip).
    /// </summary>
    [JsonIgnore]
    public Action<SyncOperation>? OperationLogger { get; set; }
}
