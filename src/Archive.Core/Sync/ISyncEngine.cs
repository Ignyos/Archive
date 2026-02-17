using Archive.Core.Domain.Entities;

namespace Archive.Core.Sync;

public interface ISyncEngine
{
    Task<SyncResult> ExecuteAsync(BackupJob job, CancellationToken cancellationToken = default);
}
