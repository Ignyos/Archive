using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;

namespace Archive.Core.Jobs;

public interface IBackupArtifactUploader
{
    BackupDestinationType DestinationType { get; }

    Task UploadAsync(
        string artifactPath,
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        AzureSqlBackupDestination destination,
        CancellationToken cancellationToken = default);
}
