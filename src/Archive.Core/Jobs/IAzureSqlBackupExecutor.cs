using Archive.Core.Domain.Entities;
using Archive.Core.Sync;

namespace Archive.Core.Jobs;

public interface IAzureSqlBackupExecutor
{
    Task<SyncResult> ExecuteAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        CancellationToken cancellationToken = default);
}
