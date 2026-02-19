using Archive.Core.Configuration;
using Archive.Core.Jobs;

namespace Archive.Desktop;

public static class NotificationPreferenceResolver
{
    public static bool ShouldNotify(ArchiveApplicationSettings settings, JobExecutionNotificationEvent notificationEvent)
    {
        if (!settings.EnableNotifications)
        {
            return false;
        }

        return notificationEvent.Kind switch
        {
            JobExecutionNotificationKind.Started => notificationEvent.NotifyOnStartOverride ?? settings.NotifyOnStart,
            JobExecutionNotificationKind.Completed => notificationEvent.NotifyOnCompleteOverride ?? settings.NotifyOnComplete,
            JobExecutionNotificationKind.Failed => notificationEvent.NotifyOnFailOverride ?? settings.NotifyOnFail,
            _ => false
        };
    }
}
