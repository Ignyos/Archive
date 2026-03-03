using Archive.Core.Domain.Entities;
using Archive.Core.Jobs;
using System.Diagnostics;
using System.Text;

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

        var arguments = new StringBuilder();
        arguments.Append("/Action:Export ");
        arguments.Append($"/SourceConnectionString:\"{EscapeForArgument(connectionString)}\" ");
        arguments.Append($"/TargetFile:\"{EscapeForArgument(artifactPath)}\"");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "SqlPackage",
                Arguments = arguments.ToString(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start SqlPackage process for Azure SQL export.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(standardError)
                ? standardOutput
                : standardError;

            throw new InvalidOperationException(
                $"SqlPackage export failed with exit code {process.ExitCode}. {message}".Trim());
        }

        if (!File.Exists(artifactPath))
        {
            throw new InvalidOperationException("SqlPackage completed but no backup artifact was produced.");
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

    private static string EscapeForArgument(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidCharacters.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "database" : sanitized;
    }
}
