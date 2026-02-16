namespace Archive.Core.Domain.Enums;

public enum SyncMode
{
    Mirror,
    Incremental
}

public enum ComparisonMethod
{
    Fast,
    Accurate
}

public enum DeletedFileHandling
{
    DeleteImmediately,
    MoveToRecycleBin
}

public enum OverwriteBehavior
{
    AlwaysOverwrite,
    KeepBoth
}

public enum TriggerType
{
    Recurring,
    OneTime,
    Manual
}

public enum JobExecutionStatus
{
    Validating,
    Running,
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public enum OperationType
{
    Copy,
    Update,
    Delete,
    Skip
}
