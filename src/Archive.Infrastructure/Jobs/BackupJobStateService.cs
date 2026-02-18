using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Jobs;

public sealed class BackupJobStateService : IBackupJobStateService
{
    private readonly ArchiveDbContext _dbContext;

    public BackupJobStateService(ArchiveDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> SetEnabledAsync(Guid jobId, bool enabled, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BackupJobs
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return false;
        }

        job.Enabled = enabled;
        job.ModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BackupJobs
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        job.DeletedAt = now;
        job.ModifiedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateBasicFieldsAsync(
        Guid jobId,
        string name,
        string? description,
        string sourcePath,
        string destinationPath,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
        {
            return false;
        }

        if (AreEquivalentPaths(sourcePath, destinationPath))
        {
            return false;
        }

        var job = await _dbContext.BackupJobs
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return false;
        }

        job.Name = name;
        job.Description = description;
        job.SourcePath = sourcePath.Trim();
        job.DestinationPath = destinationPath.Trim();
        job.Enabled = enabled;
        job.ModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool AreEquivalentPaths(string sourcePath, string destinationPath)
    {
        var normalizedSource = NormalizePathForComparison(sourcePath);
        var normalizedDestination = NormalizePathForComparison(destinationPath);
        return string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        var trimmed = path.Trim();
        return trimmed.TrimEnd('\\', '/');
    }
}