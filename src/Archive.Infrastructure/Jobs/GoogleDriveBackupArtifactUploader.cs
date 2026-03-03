using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;

namespace Archive.Infrastructure.Jobs;

public sealed class GoogleDriveBackupArtifactUploader : IBackupArtifactUploader
{
    private readonly ISecretStore _secretStore;

    public GoogleDriveBackupArtifactUploader(ISecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    public BackupDestinationType DestinationType => BackupDestinationType.GoogleDrive;

    public async Task UploadAsync(
        string artifactPath,
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        AzureSqlBackupDestination destination,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination.CredentialsSecretReference))
        {
            throw new InvalidOperationException("Google Drive destination credentials secret reference is required.");
        }

        var payload = await _secretStore.GetSecretAsync(destination.CredentialsSecretReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Google Drive destination credentials were not found in secret store.");
        }

        var credential = GoogleCredential
            .FromJson(payload)
            .CreateScoped(DriveService.Scope.DriveFile);

        using var driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Archive"
        });

        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = Path.GetFileName(artifactPath)
        };

        if (!string.IsNullOrWhiteSpace(destination.Target))
        {
            metadata.Parents = [destination.Target.Trim()];
        }

        await using var stream = File.OpenRead(artifactPath);
        var request = driveService.Files.Create(metadata, stream, "application/octet-stream");
        request.Fields = "id";

        var result = await request.UploadAsync(cancellationToken).ConfigureAwait(false);
        if (result.Status != UploadStatus.Completed)
        {
            throw new InvalidOperationException($"Google Drive upload failed with status {result.Status}.");
        }
    }
}
