using Archive.Core.Domain.Entities;

namespace Archive.Core.Jobs;

public interface IAzureSqlBacpacExporter
{
    Task<string> ExportAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        CancellationToken cancellationToken = default);
}
