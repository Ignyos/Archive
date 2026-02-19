using Archive.Core.Domain.Enums;

namespace Archive.Core.Jobs;

public enum JobExecutionNotificationKind
{
    Started,
    Completed,
    Failed
}

public sealed class JobExecutionNotificationEvent
{
    public required Guid JobId { get; init; }

    public required string JobName { get; init; }

    public required JobExecutionNotificationKind Kind { get; init; }

    public JobExecutionStatus? Status { get; init; }

    public int WarningCount { get; init; }

    public int ErrorCount { get; init; }

    public int FilesFailed { get; init; }

    public string? DetailSummary { get; init; }

    public bool? NotifyOnStartOverride { get; init; }

    public bool? NotifyOnCompleteOverride { get; init; }

    public bool? NotifyOnFailOverride { get; init; }
}

public static class JobExecutionNotificationHub
{
    public static event Action<JobExecutionNotificationEvent>? Published;

    public static void Publish(JobExecutionNotificationEvent notificationEvent)
    {
        Published?.Invoke(notificationEvent);
    }
}