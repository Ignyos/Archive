using Archive.Core.Domain.Enums;

namespace Archive.Core.Domain.Entities;

public sealed class AzureSqlBackupDestination
{
    public Guid Id { get; set; }

    public Guid AzureSqlBackupJobId { get; set; }

    public BackupDestinationType DestinationType { get; set; }

    public string Target { get; set; } = string.Empty;

    public string? AccountOrDriveIdentifier { get; set; }

    public string? CredentialsSecretReference { get; set; }

    public AzureSqlBackupJob AzureSqlBackupJob { get; set; } = null!;
}
