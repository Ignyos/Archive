using Archive.Core.Domain.Enums;

namespace Archive.Core.Domain.Entities;

public sealed class ExecutionLog
{
    public Guid Id { get; set; }

    public Guid JobExecutionId { get; set; }

    public JobExecution? Execution { get; set; }

    public DateTime Timestamp { get; set; }

    public LogLevel Level { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? FilePath { get; set; }

    public OperationType? OperationType { get; set; }

    public string? ExceptionDetails { get; set; }
}
