namespace Archive.Core.Sync;

public sealed class SourceScanState
{
    public Guid JobId { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
