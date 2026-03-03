using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Azure.Storage.Blobs;

namespace Archive.Infrastructure.Jobs;

public sealed class AzureBlobBackupArtifactUploader : IBackupArtifactUploader
{
    private readonly ISecretStore _secretStore;

    public AzureBlobBackupArtifactUploader(ISecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    public BackupDestinationType DestinationType => BackupDestinationType.AzureBlobStorage;

    public async Task UploadAsync(
        string artifactPath,
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        AzureSqlBackupDestination destination,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination.CredentialsSecretReference))
        {
            throw new InvalidOperationException("Azure Blob destination credentials secret reference is required.");
        }

        var payload = await _secretStore.GetSecretAsync(destination.CredentialsSecretReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Azure Blob destination credentials were not found in secret store.");
        }

        var (containerName, prefix) = ResolveContainerAndPrefix(destination.Target);
        var blobName = string.IsNullOrWhiteSpace(prefix)
            ? Path.GetFileName(artifactPath)
            : $"{prefix.TrimEnd('/')}/{Path.GetFileName(artifactPath)}";

        var connectionString = SecretPayloadParser.TryReadStringField(payload, "connectionString") ?? payload;
        var containerClient = new BlobContainerClient(connectionString, containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobClient = containerClient.GetBlobClient(blobName);
        await using var stream = File.OpenRead(artifactPath);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken).ConfigureAwait(false);
    }

    private static (string ContainerName, string Prefix) ResolveContainerAndPrefix(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Azure Blob destination target is required. Expected format: container[/optional/prefix].");
        }

        var parts = target
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new InvalidOperationException("Azure Blob destination target is invalid.");
        }

        var containerName = parts[0];
        var prefix = parts.Length > 1
            ? string.Join('/', parts.Skip(1))
            : string.Empty;

        return (containerName, prefix);
    }
}
