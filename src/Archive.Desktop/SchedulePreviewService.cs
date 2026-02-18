using System.Text;
using Archive.Core.Domain.Enums;
using Quartz;

namespace Archive.Desktop;

public static class SchedulePreviewService
{
    public static string Build(
        TriggerType triggerType,
        string? cronExpression,
        DateTime? oneTimeLocal,
        DateTime? nowLocal = null)
    {
        var localNow = nowLocal ?? DateTime.Now;

        switch (triggerType)
        {
            case TriggerType.Manual:
                return "No automatic schedule (manual only).";

            case TriggerType.Recurring:
            {
                var cron = cronExpression?.Trim();
                if (string.IsNullOrWhiteSpace(cron) || !CronExpression.IsValidExpression(cron))
                {
                    return "Enter a valid cron expression to preview next runs.";
                }

                var expression = new CronExpression(cron);
                var cursor = localNow.Kind == DateTimeKind.Utc
                    ? localNow
                    : localNow.ToUniversalTime();

                var runs = new List<DateTime>();
                for (var index = 0; index < 5; index++)
                {
                    var next = expression.GetNextValidTimeAfter(new DateTimeOffset(cursor));
                    if (!next.HasValue)
                    {
                        break;
                    }

                    runs.Add(next.Value.LocalDateTime);
                    cursor = next.Value.UtcDateTime;
                }

                if (runs.Count == 0)
                {
                    return "No upcoming runs were found for this cron expression.";
                }

                var builder = new StringBuilder();
                builder.AppendLine("Next 5 runs:");
                foreach (var run in runs)
                {
                    builder.AppendLine($"- {run:yyyy-MM-dd HH:mm}");
                }

                return builder.ToString().TrimEnd();
            }

            case TriggerType.OneTime:
            {
                if (!oneTimeLocal.HasValue)
                {
                    return "Select a one-time date and time to preview.";
                }

                if (oneTimeLocal.Value <= localNow)
                {
                    return "One-time run must be in the future.";
                }

                return $"Next run: {oneTimeLocal.Value:yyyy-MM-dd HH:mm}";
            }

            default:
                return "Schedule preview unavailable.";
        }
    }
}