using Archive.Core.Domain.Entities;
using Archive.Core.Jobs;
using Microsoft.SqlServer.Dac;

namespace Archive.Infrastructure.Jobs;

public sealed class AzureSqlBacpacExporter : IAzureSqlBacpacExporter
{
    private readonly ISecretStore _secretStore;

    public AzureSqlBacpacExporter(ISecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    public async Task<string> ExportAsync(
        BackupJob job,
        AzureSqlBackupJob azureSqlBackupJob,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(azureSqlBackupJob.DatabaseName))
        {
            throw new InvalidOperationException("DatabaseName is required for Azure SQL backup jobs.");
        }

        if (string.IsNullOrWhiteSpace(azureSqlBackupJob.CredentialsSecretReference))
        {
            throw new InvalidOperationException("Azure SQL backup credentials secret reference is required.");
        }

        var secretPayload = await _secretStore
            .GetSecretAsync(azureSqlBackupJob.CredentialsSecretReference, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(secretPayload))
        {
            throw new InvalidOperationException("Azure SQL backup credentials were not found in secret store.");
        }

        var connectionString = ResolveConnectionString(secretPayload);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Azure SQL backup secret payload does not contain a usable connection string.");
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "Archive", "AzureSqlBackups", job.Id.ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        var artifactPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(azureSqlBackupJob.DatabaseName)}_{timestamp}.bacpac");

        try
        {
            await Task.Run(() =>
            {
                var dacServices = new DacServices(connectionString);
                dacServices.ExportBacpac(artifactPath, azureSqlBackupJob.DatabaseName);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Azure SQL export failed for database '{azureSqlBackupJob.DatabaseName}'.", ex);
        }

        if (!File.Exists(artifactPath))
        {
            throw new InvalidOperationException("Azure SQL export completed but no backup artifact was produced.");
        }

        return artifactPath;
    }

    private static string? ResolveConnectionString(string secretPayload)
    {
        var explicitConnectionString = SecretPayloadParser.TryReadStringField(secretPayload, "connectionString")
                                       ?? SecretPayloadParser.TryReadStringField(secretPayload, "sourceConnectionString");

        return string.IsNullOrWhiteSpace(explicitConnectionString)
            ? secretPayload.Trim()
            : explicitConnectionString.Trim();
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidCharacters.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "database" : sanitized;
    }
}
