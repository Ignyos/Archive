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

        if (latestExecutionStatus.HasValue)
        {
            return latestExecutionStatus.Value switch
            {
                JobExecutionStatus.Running => "Running",
                JobExecutionStatus.Validating => "Validating",
                JobExecutionStatus.CompletedWithWarnings => "Warning",
                JobExecutionStatus.Failed => "Error",
                JobExecutionStatus.Completed => "Completed",
                JobExecutionStatus.Cancelled => "Cancelled",
                _ => latestExecutionStatus.Value.ToString()
            };
        }

        var isScheduled = enabled && triggerType != TriggerType.Manual;
        return isScheduled ? "Scheduled" : "Idle";
    }
}