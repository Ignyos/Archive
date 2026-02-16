using Archive.Core.Domain.Enums;

namespace Archive.Core.Domain.Entities;

public sealed class BackupJob
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public SyncMode SyncMode { get; set; }

    public ComparisonMethod ComparisonMethod { get; set; }

    public DeletedFileHandling? DeletedFileHandling { get; set; }

    public OverwriteBehavior OverwriteBehavior { get; set; }

    public Guid? SyncOptionsId { get; set; }

    public SyncOptions? SyncOptions { get; set; }

    public TriggerType TriggerType { get; set; }

    public string? CronExpression { get; set; }

    public DateTime? SimpleTriggerTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public DateTime? LastRunAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public bool? NotifyOnStart { get; set; }

    public bool? NotifyOnComplete { get; set; }

    public bool? NotifyOnFail { get; set; }

    public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();

    public ICollection<BackupJobExclusionPattern> BackupJobExclusionPatterns { get; set; } = new List<BackupJobExclusionPattern>();
}
