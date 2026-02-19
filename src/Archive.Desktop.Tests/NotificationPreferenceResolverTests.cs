using Archive.Core.Configuration;
using Archive.Core.Jobs;

namespace Archive.Desktop.Tests;

public sealed class NotificationPreferenceResolverTests
{
    [Fact]
    public void ShouldNotify_ReturnsFalse_WhenGlobalNotificationsDisabled()
    {
        var settings = new ArchiveApplicationSettings
        {
            EnableNotifications = false,
            NotifyOnStart = true,
            NotifyOnComplete = true,
            NotifyOnFail = true
        };

        var evt = BuildEvent(JobExecutionNotificationKind.Started);

        Assert.False(NotificationPreferenceResolver.ShouldNotify(settings, evt));
    }

    [Fact]
    public void ShouldNotify_UsesGlobalSetting_WhenNoOverrideProvided()
    {
        var settings = new ArchiveApplicationSettings
        {
            EnableNotifications = true,
            NotifyOnStart = false,
            NotifyOnComplete = true,
            NotifyOnFail = true
        };

        var evt = BuildEvent(JobExecutionNotificationKind.Started);

        Assert.False(NotificationPreferenceResolver.ShouldNotify(settings, evt));
    }

    [Fact]
    public void ShouldNotify_UsesOverride_WhenProvided()
    {
        var settings = new ArchiveApplicationSettings
        {
            EnableNotifications = true,
            NotifyOnStart = false,
            NotifyOnComplete = true,
            NotifyOnFail = true
        };

        var evt = BuildEvent(JobExecutionNotificationKind.Started, notifyOnStartOverride: true);

        Assert.True(NotificationPreferenceResolver.ShouldNotify(settings, evt));
    }

    [Fact]
    public void ShouldNotify_UsesFailOverrideForFailedKind()
    {
        var settings = new ArchiveApplicationSettings
        {
            EnableNotifications = true,
            NotifyOnStart = false,
            NotifyOnComplete = false,
            NotifyOnFail = false
        };

        var evt = BuildEvent(JobExecutionNotificationKind.Failed, notifyOnFailOverride: true);

        Assert.True(NotificationPreferenceResolver.ShouldNotify(settings, evt));
    }

    private static JobExecutionNotificationEvent BuildEvent(
        JobExecutionNotificationKind kind,
        bool? notifyOnStartOverride = null,
        bool? notifyOnCompleteOverride = null,
        bool? notifyOnFailOverride = null)
    {
        return new JobExecutionNotificationEvent
        {
            JobId = Guid.NewGuid(),
            JobName = "Job",
            Kind = kind,
            NotifyOnStartOverride = notifyOnStartOverride,
            NotifyOnCompleteOverride = notifyOnCompleteOverride,
            NotifyOnFailOverride = notifyOnFailOverride
        };
    }
}
