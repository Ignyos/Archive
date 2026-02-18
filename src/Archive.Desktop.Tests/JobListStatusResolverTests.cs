using Archive.Core.Domain.Enums;

namespace Archive.Desktop.Tests;

public class JobListStatusResolverTests
{
    [Fact]
    public void Resolve_ReturnsError_WhenLatestExecutionFailed()
    {
        var status = JobListStatusResolver.Resolve(
            enabled: true,
            triggerType: TriggerType.Recurring,
            latestExecutionStatus: JobExecutionStatus.Failed);

        Assert.Equal("Error", status);
    }

    [Fact]
    public void Resolve_ReturnsWarning_WhenLatestExecutionCompletedWithWarnings()
    {
        var status = JobListStatusResolver.Resolve(
            enabled: true,
            triggerType: TriggerType.Recurring,
            latestExecutionStatus: JobExecutionStatus.CompletedWithWarnings);

        Assert.Equal("Warning", status);
    }

    [Fact]
    public void Resolve_ReturnsRunning_WhenLatestExecutionRunning()
    {
        var status = JobListStatusResolver.Resolve(
            enabled: true,
            triggerType: TriggerType.Recurring,
            latestExecutionStatus: JobExecutionStatus.Running);

        Assert.Equal("Running", status);
    }

    [Fact]
    public void Resolve_ReturnsRunning_WhenRuntimeIndicatesCurrentlyExecuting()
    {
        var status = JobListStatusResolver.Resolve(
            enabled: true,
            triggerType: TriggerType.Recurring,
            latestExecutionStatus: JobExecutionStatus.Completed,
            isCurrentlyRunning: true);

        Assert.Equal("Running", status);
    }

    [Fact]
    public void Resolve_ReturnsScheduled_WhenEnabledAndScheduledAndNoFailureState()
    {
        var status = JobListStatusResolver.Resolve(
            enabled: true,
            triggerType: TriggerType.Recurring,
            latestExecutionStatus: JobExecutionStatus.Completed);

        Assert.Equal("Scheduled", status);
    }

    [Fact]
    public void Resolve_ReturnsIdle_WhenManualOrDisabled()
    {
        var manualStatus = JobListStatusResolver.Resolve(
            enabled: true,
            triggerType: TriggerType.Manual,
            latestExecutionStatus: null);

        var disabledStatus = JobListStatusResolver.Resolve(
            enabled: false,
            triggerType: TriggerType.Recurring,
            latestExecutionStatus: null);

        Assert.Equal("Idle", manualStatus);
        Assert.Equal("Idle", disabledStatus);
    }
}