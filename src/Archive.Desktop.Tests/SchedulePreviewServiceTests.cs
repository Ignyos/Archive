using Archive.Core.Domain.Enums;

namespace Archive.Desktop.Tests;

public class SchedulePreviewServiceTests
{
    [Fact]
    public void Build_ReturnsManualMessage_ForManualTrigger()
    {
        var result = SchedulePreviewService.Build(
            TriggerType.Manual,
            cronExpression: null,
            oneTimeLocal: null,
            nowLocal: new DateTime(2026, 2, 18, 10, 0, 0));

        Assert.Equal("No automatic schedule (manual only).", result);
    }

    [Fact]
    public void Build_ReturnsValidationMessage_ForInvalidCron()
    {
        var result = SchedulePreviewService.Build(
            TriggerType.Recurring,
            cronExpression: "invalid",
            oneTimeLocal: null,
            nowLocal: new DateTime(2026, 2, 18, 10, 0, 0));

        Assert.Equal("Enter a valid cron expression to preview next runs.", result);
    }

    [Fact]
    public void Build_ReturnsNextRuns_ForValidCron()
    {
        var result = SchedulePreviewService.Build(
            TriggerType.Recurring,
            cronExpression: "0 0 2 * * ?",
            oneTimeLocal: null,
            nowLocal: new DateTime(2026, 2, 18, 10, 0, 0));

        Assert.Contains("Next 5 runs:", result);
    }

    [Fact]
    public void Build_ReturnsValidationMessage_ForOneTimeInPast()
    {
        var result = SchedulePreviewService.Build(
            TriggerType.OneTime,
            cronExpression: null,
            oneTimeLocal: new DateTime(2026, 2, 18, 9, 0, 0),
            nowLocal: new DateTime(2026, 2, 18, 10, 0, 0));

        Assert.Equal("One-time run must be in the future.", result);
    }
}
