namespace Archive.Core.Domain.Entities;

public sealed class SyncOptions
{
    public Guid Id { get; set; }

    public bool Recursive { get; set; } = true;

    public bool DeleteOrphaned { get; set; }

    public bool VerifyAfterCopy { get; set; }

    public bool SkipHiddenAndSystem { get; set; } = true;

    public ICollection<BackupJob> BackupJobs { get; set; } = new List<BackupJob>();
}
