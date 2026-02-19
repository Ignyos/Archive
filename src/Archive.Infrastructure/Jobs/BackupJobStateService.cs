using Archive.Core.Jobs;
using Archive.Core.Domain.Enums;
using Archive.Core.Domain.Entities;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.IO;

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
        TriggerType triggerType,
        string? cronExpression,
        DateTime? simpleTriggerTime,
        bool recursive = true,
        bool deleteOrphaned = false,
        bool skipHiddenAndSystem = true,
        bool verifyAfterCopy = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
        {
            return false;
        }

        if (AreEquivalentPaths(sourcePath, destinationPath))
        {
            return false;
        }

        if (IsDestinationNestedWithinSource(sourcePath, destinationPath))
        {
            return false;
        }

        if (!TryNormalizeSchedule(triggerType, cronExpression, simpleTriggerTime, out var normalizedCron, out var normalizedOneTimeUtc))
        {
            return false;
        }

        var normalizedName = name.Trim();

        var duplicateNameExists = await _dbContext.BackupJobs
            .AnyAsync(
                x => x.Id != jobId
                    && x.DeletedAt == null
                    && x.Name != null
                    && x.Name.ToLower() == normalizedName.ToLower(),
                cancellationToken);

        if (duplicateNameExists)
        {
            return false;
        }

        var job = await _dbContext.BackupJobs
            .Include(x => x.SyncOptions)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return false;
        }

        job.Name = normalizedName;
        job.Description = description;
        job.SourcePath = sourcePath.Trim();
        job.DestinationPath = destinationPath.Trim();
        job.Enabled = enabled;
        job.TriggerType = triggerType;
        job.CronExpression = normalizedCron;
        job.SimpleTriggerTime = normalizedOneTimeUtc;

        var syncOptions = EnsureSyncOptions(job);
        syncOptions.Recursive = recursive;
        syncOptions.DeleteOrphaned = deleteOrphaned;
        syncOptions.SkipHiddenAndSystem = skipHiddenAndSystem;
        syncOptions.VerifyAfterCopy = verifyAfterCopy;

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

    private static bool IsDestinationNestedWithinSource(string sourcePath, string destinationPath)
    {
        var normalizedSource = NormalizePathForComparison(sourcePath);
        var normalizedDestination = NormalizePathForComparison(destinationPath);

        if (string.IsNullOrWhiteSpace(normalizedSource) || string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return false;
        }

        return normalizedDestination.StartsWith(
            normalizedSource + "\\",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        var trimmed = path.Trim();

        try
        {
            return Path.GetFullPath(trimmed).TrimEnd('\\', '/');
        }
        catch
        {
            return trimmed.TrimEnd('\\', '/');
        }
    }

    private SyncOptions EnsureSyncOptions(BackupJob job)
    {
        if (job.SyncOptions is not null)
        {
            return job.SyncOptions;
        }

        var syncOptions = new SyncOptions
        {
            Id = Guid.NewGuid()
        };

        _dbContext.SyncOptions.Add(syncOptions);
        job.SyncOptions = syncOptions;
        job.SyncOptionsId = syncOptions.Id;
        return syncOptions;
    }

    private static bool TryNormalizeSchedule(
        TriggerType triggerType,
        string? cronExpression,
        DateTime? simpleTriggerTime,
        out string? normalizedCron,
        out DateTime? normalizedOneTimeUtc)
    {
        normalizedCron = null;
        normalizedOneTimeUtc = null;

        switch (triggerType)
        {
            case TriggerType.Manual:
                return true;

            case TriggerType.Recurring:
            {
                var trimmedCron = cronExpression?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedCron) || !CronExpression.IsValidExpression(trimmedCron))
                {
                    return false;
                }

                normalizedCron = trimmedCron;
                return true;
            }

            case TriggerType.OneTime:
            {
                if (!simpleTriggerTime.HasValue)
                {
                    return false;
                }

                var oneTimeUtc = simpleTriggerTime.Value.Kind == DateTimeKind.Utc
                    ? simpleTriggerTime.Value
                    : simpleTriggerTime.Value.ToUniversalTime();

                if (oneTimeUtc <= DateTime.UtcNow)
                {
                    return false;
                }

                normalizedOneTimeUtc = oneTimeUtc;
                return true;
            }

            default:
                return false;
        }
    }
}