using Archive.Core.Domain.Enums;

namespace Archive.Desktop;

public static class ExecutionDisplayFormatter
{
    public static string FormatStatus(
        JobExecutionStatus status,
        int warningCount,
        int errorCount,
        int filesFailed)
    {
        if (status == JobExecutionStatus.CompletedWithWarnings)
        {
            return "Warning";
        }

        if (status == JobExecutionStatus.Completed && (warningCount > 0 || errorCount > 0 || filesFailed > 0))
        {
            return "Warning";
        }

        return status switch
        {
            JobExecutionStatus.Completed => "Completed",
            JobExecutionStatus.Failed => "Failed",
            JobExecutionStatus.Cancelled => "Cancelled",
            JobExecutionStatus.Running => "Running",
            JobExecutionStatus.Validating => "Validating",
            _ => status.ToString()
        };
    }

    public static string FormatTimestamp(DateTime? utcTimestamp)
    {
        if (!utcTimestamp.HasValue)
        {
            return "-";
        }

        return utcTimestamp.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static string FormatDuration(TimeSpan? duration)
    {
        return duration.HasValue
            ? duration.Value.ToString("hh\\:mm\\:ss")
            : "-";
    }
}
