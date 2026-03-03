using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;

namespace Archive.Infrastructure.Jobs;

public sealed class LocalDeviceBackupArtifactUploader : IBackupArtifactUploader
{
    public BackupDestinationType DestinationType => BackupDestinationType.LocalDevice;

    public Task UploadAsync(
        string artifactPath,
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        AzureSqlBackupDestination destination,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination.Target))
        {
            throw new InvalidOperationException("Local destination target path is required.");
        }

        if (!File.Exists(artifactPath))
        {
            throw new FileNotFoundException("Backup artifact was not found.", artifactPath);
        }

        Directory.CreateDirectory(destination.Target);

        var destinationPath = Path.Combine(destination.Target, Path.GetFileName(artifactPath));
        File.Copy(artifactPath, destinationPath, overwrite: true);

        return Task.CompletedTask;
    }
}
