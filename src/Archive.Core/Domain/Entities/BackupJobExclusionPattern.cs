namespace Archive.Core.Domain.Entities;

public sealed class BackupJobExclusionPattern
{
    public Guid BackupJobId { get; set; }

    public BackupJob? BackupJob { get; set; }

    public Guid ExclusionPatternId { get; set; }

    public ExclusionPattern? ExclusionPattern { get; set; }
}
