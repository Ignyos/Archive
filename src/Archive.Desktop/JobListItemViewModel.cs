using Archive.Core.Domain.Enums;

namespace Archive.Desktop;

public sealed class JobListItemViewModel
{
    public Guid Id { get; init; }

    public string Status { get; set; } = "Idle";

    public bool Enabled { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public TriggerType TriggerType { get; set; }

    public string? CronExpression { get; set; }

    public DateTime? SimpleTriggerTime { get; set; }

    public bool Recursive { get; set; } = true;

    public bool DeleteOrphaned { get; set; }

    public bool SkipHiddenAndSystem { get; set; } = true;

    public bool VerifyAfterCopy { get; set; }

    public SyncMode SyncMode { get; set; }

    public ComparisonMethod ComparisonMethod { get; set; }

    public OverwriteBehavior OverwriteBehavior { get; set; }

    public DateTime? NextRun { get; set; }
}