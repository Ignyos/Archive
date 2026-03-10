using Archive.Core.Domain.Enums;

namespace Archive.Core.Domain.Entities;

public sealed class AzureSqlBackupWorkflowAction
{
    public Guid Id { get; set; }

    public Guid AzureSqlBackupJobId { get; set; }

    public int StepOrder { get; set; }

    public AzureSqlWorkflowActionType ActionType { get; set; }

    public Guid? AzureSqlBackupDestinationId { get; set; }

    public string? ConfigurationJson { get; set; }

    public AzureSqlBackupJob AzureSqlBackupJob { get; set; } = null!;

    public AzureSqlBackupDestination? AzureSqlBackupDestination { get; set; }
}
