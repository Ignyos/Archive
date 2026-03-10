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
        var workflowActions = azureSqlBackupJob.WorkflowActions
            .OrderBy(x => x.StepOrder)
            .ToList();

        if (workflowActions.Count > 0)
        {
            return await ExecuteWorkflowActionsAsync(job, azureSqlBackupJob, workflowActions, cancellationToken).ConfigureAwait(false);
        }

        if (azureSqlBackupJob.Destinations.Count == 0)
        {
            throw new InvalidOperationException("At least one destination is required for Azure SQL backup jobs.");
        }

        return await ExecuteLegacyDestinationsAsync(job, azureSqlBackupJob, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SyncResult> ExecuteWorkflowActionsAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        IReadOnlyList<AzureSqlBackupWorkflowAction> workflowActions,
        CancellationToken cancellationToken)
    {
        var successfulUploads = 0;
        var failedUploads = 0;
        var totalBytesTransferred = 0L;
        Exception? lastUploadException = null;

        foreach (var action in workflowActions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (action.ActionType)
            {
                case AzureSqlWorkflowActionType.AzureSqlExportToDestination:
                {
                    var destination = ResolveDestinationForAction(azureSqlBackupJob, action);
                    if (destination is null)
                    {
                        failedUploads++;
                        lastUploadException = new InvalidOperationException($"Workflow action '{action.Id}' did not resolve to a destination.");
                        continue;
                    }

                    var stepResult = await ExecuteSingleDestinationUploadAsync(job, azureSqlBackupJob, destination, cancellationToken)
                        .ConfigureAwait(false);

                    successfulUploads += stepResult.SuccessfulUploads;
                    failedUploads += stepResult.FailedUploads;
                    totalBytesTransferred += stepResult.BytesTransferred;
                    lastUploadException = stepResult.LastException ?? lastUploadException;
                    break;
                }

                case AzureSqlWorkflowActionType.BlobToLocalCopy:
                case AzureSqlWorkflowActionType.BlobToGoogleDriveCopy:
                    throw new NotSupportedException($"Workflow action type '{action.ActionType}' is not implemented yet.");

                default:
                    throw new InvalidOperationException($"Unsupported workflow action type '{action.ActionType}'.");
            }
        }

        if (successfulUploads == 0)
        {
            throw new InvalidOperationException("All workflow actions failed.", lastUploadException);
        }

        return new SyncResult
        {
            FilesScanned = workflowActions.Count,
            FilesCopied = successfulUploads,
            FilesFailed = failedUploads,
            FilesSkipped = 0,
            FilesUpdated = 0,
            FilesDeleted = 0,
            BytesTransferred = totalBytesTransferred,
            ErrorCount = 0,
            WarningCount = failedUploads
        };
    }

    private async Task<SyncResult> ExecuteLegacyDestinationsAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        CancellationToken cancellationToken)
    {
        var successfulUploads = 0;
        var failedUploads = 0;
        var totalBytesTransferred = 0L;
        Exception? lastUploadException = null;

        foreach (var destination in azureSqlBackupJob.Destinations)
        {
            var stepResult = await ExecuteSingleDestinationUploadAsync(job, azureSqlBackupJob, destination, cancellationToken)
                .ConfigureAwait(false);

            successfulUploads += stepResult.SuccessfulUploads;
            failedUploads += stepResult.FailedUploads;
            totalBytesTransferred += stepResult.BytesTransferred;
            lastUploadException = stepResult.LastException ?? lastUploadException;
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
            BytesTransferred = totalBytesTransferred,
            ErrorCount = 0,
            WarningCount = failedUploads
        };
    }

    private async Task<(int SuccessfulUploads, int FailedUploads, long BytesTransferred, Exception? LastException)> ExecuteSingleDestinationUploadAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        AzureSqlBackupDestination destination,
        CancellationToken cancellationToken)
    {
        var artifactPath = await _exporter.ExportAsync(job, azureSqlBackupJob, cancellationToken).ConfigureAwait(false);
        var artifactBytes = new FileInfo(artifactPath).Length;
        var successfulUploads = 0;
        var failedUploads = 0;
        Exception? lastUploadException = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_uploaders.TryGetValue(destination.DestinationType, out var uploader))
            {
                failedUploads++;
                lastUploadException = new InvalidOperationException($"No uploader is registered for destination type '{destination.DestinationType}'.");
            }
            else
            {
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

        return (successfulUploads, failedUploads, artifactBytes * successfulUploads, lastUploadException);
    }

    private static AzureSqlBackupDestination? ResolveDestinationForAction(
        AzureSqlBackupJob azureSqlBackupJob,
        AzureSqlBackupWorkflowAction workflowAction)
    {
        if (workflowAction.AzureSqlBackupDestination is not null)
        {
            return workflowAction.AzureSqlBackupDestination;
        }

        if (workflowAction.AzureSqlBackupDestinationId.HasValue)
        {
            return azureSqlBackupJob.Destinations.FirstOrDefault(x => x.Id == workflowAction.AzureSqlBackupDestinationId.Value);
        }

        return azureSqlBackupJob.Destinations.FirstOrDefault();
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
