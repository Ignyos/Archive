namespace Archive.Core.Configuration;

public sealed class AppSettings
{
    public ArchiveSettings Archive { get; init; } = new();

    public QuartzSettings Quartz { get; init; } = new();
}

public sealed class ArchiveSettings
{
    public int MaxConcurrentJobs { get; init; } = 1;

    public bool ArchiveScheduleEnabled { get; init; } = true;
}

public sealed class QuartzSettings
{
    public bool UseInMemoryStore { get; init; }

    public bool UseSQLite { get; init; }

    public string ConnectionString { get; init; } = string.Empty;

    public int MaxConcurrency { get; init; } = 10;

    public string MisfireThreshold { get; init; } = "00:01:00";
}
