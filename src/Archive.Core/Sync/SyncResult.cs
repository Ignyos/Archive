namespace Archive.Core.Sync;

public sealed class SyncResult
{
    public int FilesScanned { get; init; }

    public int FilesCopied { get; init; }

    public int FilesUpdated { get; init; }

    public int FilesDeleted { get; init; }

    public int FilesSkipped { get; init; }

    public int FilesFailed { get; init; }

    public long BytesTransferred { get; init; }

    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }
}
