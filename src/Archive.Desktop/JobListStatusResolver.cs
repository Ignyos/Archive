using Archive.Core.Domain.Enums;

namespace Archive.Desktop;

public static class JobListStatusResolver
{
    public static string Resolve(
        bool enabled,
        TriggerType triggerType,
        JobExecutionStatus? latestExecutionStatus,
        bool isCurrentlyRunning = false)
    {
        if (isCurrentlyRunning)
        {
            return "Running";
        }

        if (latestExecutionStatus == JobExecutionStatus.Running)
        {
            return "Running";
        }

        if (latestExecutionStatus == JobExecutionStatus.CompletedWithWarnings)
        {
            return "Warning";
        }

        if (latestExecutionStatus == JobExecutionStatus.Failed)
        {
            return "Error";
        }

        var isScheduled = enabled && triggerType != TriggerType.Manual;
        return isScheduled ? "Scheduled" : "Idle";
    }
}