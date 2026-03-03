using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Core.Sync;

namespace Archive.Infrastructure.Jobs;

public sealed class AzureSqlBackupExecutor : IAzureSqlBackupExecutor
{
    private readonly IAzureSqlBacpacExporter _exporter;
    private readonly IReadOnlyDictionary<BackupDestinationType, IBackupArtifactUploader> _uploaders;

    public AzureSqlBackupExecutor(
        IAzureSqlBacpacExporter exporter,
        IEnumerable<IBackupArtifactUploader> uploaders)
    {
        _exporter = exporter;
        _uploaders = uploaders.ToDictionary(x => x.DestinationType);
    }

    public Task<SyncResult> ExecuteAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInternalAsync(job, azureSqlBackupJob, cancellationToken);
    }

    private async Task<SyncResult> ExecuteInternalAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        CancellationToken cancellationToken)
    {
        if (azureSqlBackupJob.Destinations.Count == 0)
        {
            throw new InvalidOperationException("At least one destination is required for Azure SQL backup jobs.");
        }

        var artifactPath = await _exporter.ExportAsync(job, azureSqlBackupJob, cancellationToken).ConfigureAwait(false);
        var artifactBytes = new FileInfo(artifactPath).Length;

        var successfulUploads = 0;
        var failedUploads = 0;

        Exception? lastUploadException = null;

        try
        {
            foreach (var destination in azureSqlBackupJob.Destinations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_uploaders.TryGetValue(destination.DestinationType, out var uploader))
                {
                    failedUploads++;
                    lastUploadException = new InvalidOperationException($"No uploader is registered for destination type '{destination.DestinationType}'.");
                    continue;
                }

                try
                {
                    await uploader
                        .UploadAsync(artifactPath, job, azureSqlBackupJob, destination, cancellationToken)
                        .ConfigureAwait(false);

                    successfulUploads++;
                }
                catch (Exception ex)
                {
                    failedUploads++;
                    lastUploadException = ex;
                }
            }
        }
        finally
        {
            TryDeleteTemporaryArtifact(artifactPath);
        }

        if (successfulUploads == 0)
        {
            throw new InvalidOperationException("All backup destination uploads failed.", lastUploadException);
        }

        return new SyncResult
        {
            FilesScanned = 1,
            FilesCopied = successfulUploads,
            FilesFailed = failedUploads,
            FilesSkipped = 0,
            FilesUpdated = 0,
            FilesDeleted = 0,
            BytesTransferred = artifactBytes * successfulUploads,
            ErrorCount = 0,
            WarningCount = failedUploads
        };
    }

    private static void TryDeleteTemporaryArtifact(string artifactPath)
    {
        try
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }
        }
        catch
        {
        }
    }
}
