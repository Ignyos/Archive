namespace Archive.Core.Configuration;

public enum LogRetentionUnit
{
    Days,
    Months
}

public sealed class ArchiveApplicationSettings
{
    public bool RunOnWindowsStartup { get; init; }

    public bool EnableNotifications { get; init; } = true;

    public bool NotifyOnStart { get; init; }

    public bool NotifyOnComplete { get; init; } = true;

    public bool NotifyOnFail { get; init; } = true;

    public bool PlayNotificationSound { get; init; } = true;

    public int LogRetentionValue { get; init; } = 14;

    public LogRetentionUnit LogRetentionUnit { get; init; } = LogRetentionUnit.Days;

    public bool EnableVerboseLogging { get; init; }
}