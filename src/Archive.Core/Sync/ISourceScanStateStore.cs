namespace Archive.Core.Sync;

public interface ISourceScanStateStore
{
    Task<SourceScanState?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task SaveAsync(SourceScanState state, CancellationToken cancellationToken = default);
}
