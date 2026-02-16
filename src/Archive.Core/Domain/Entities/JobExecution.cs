using Archive.Core.Domain.Enums;

namespace Archive.Core.Domain.Entities;

public sealed class JobExecution
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public BackupJob? Job { get; set; }

    public JobExecutionStatus Status { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public TimeSpan? Duration { get; set; }

    public int FilesScanned { get; set; }

    public int FilesCopied { get; set; }

    public int FilesUpdated { get; set; }

    public int FilesDeleted { get; set; }

    public int FilesSkipped { get; set; }

    public int FilesFailed { get; set; }

    public long BytesTransferred { get; set; }

    public int ErrorCount { get; set; }

    public int WarningCount { get; set; }

    public ICollection<ExecutionLog> Logs { get; set; } = new List<ExecutionLog>();
}
