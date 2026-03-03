namespace Archive.Core.Domain.Entities;

public sealed class AzureSqlBackupJob
{
    public Guid JobId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string? ResourceGroupName { get; set; }

    public string? SubscriptionId { get; set; }

    public string? CredentialsSecretReference { get; set; }

    public BackupJob Job { get; set; } = null!;

    public ICollection<AzureSqlBackupDestination> Destinations { get; set; } = new List<AzureSqlBackupDestination>();
}
