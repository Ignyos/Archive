using Archive.Core.Jobs;

namespace Archive.Desktop.Tests;

public sealed class NotificationRateLimiterTests
{
    [Fact]
    public void ShouldShow_ReturnsFalse_ForDuplicateWithinDedupeWindow()
    {
        var limiter = new NotificationRateLimiter(
            minimumInterval: TimeSpan.Zero,
            dedupeWindow: TimeSpan.FromSeconds(20));

        var now = DateTime.UtcNow;
        var evt = BuildEvent();

        Assert.True(limiter.ShouldShow(evt, now));
        Assert.False(limiter.ShouldShow(evt, now.AddSeconds(5)));
    }

    [Fact]
    public void ShouldShow_ReturnsTrue_ForDuplicateAfterDedupeWindow()
    {
        var limiter = new NotificationRateLimiter(
            minimumInterval: TimeSpan.Zero,
            dedupeWindow: TimeSpan.FromSeconds(10));

        var now = DateTime.UtcNow;
        var evt = BuildEvent();

        Assert.True(limiter.ShouldShow(evt, now));
        Assert.True(limiter.ShouldShow(evt, now.AddSeconds(11)));
    }

    [Fact]
    public void ShouldShow_AppliesGlobalMinimumInterval()
    {
        var limiter = new NotificationRateLimiter(
            minimumInterval: TimeSpan.FromSeconds(2),
            dedupeWindow: TimeSpan.FromSeconds(20));

        var now = DateTime.UtcNow;
        var first = BuildEvent(kind: JobExecutionNotificationKind.Started);
        var second = BuildEvent(kind: JobExecutionNotificationKind.Completed);

        Assert.True(limiter.ShouldShow(first, now));
        Assert.False(limiter.ShouldShow(second, now.AddMilliseconds(800)));
        Assert.True(limiter.ShouldShow(second, now.AddSeconds(3)));
    }

    private static JobExecutionNotificationEvent BuildEvent(JobExecutionNotificationKind kind = JobExecutionNotificationKind.Failed)
    {
        return new JobExecutionNotificationEvent
        {
            JobId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            JobName = "Job",
            Kind = kind,
            DetailSummary = "detail"
        };
    }
}
