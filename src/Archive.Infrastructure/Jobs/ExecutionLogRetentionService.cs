using Archive.Core.Configuration;
using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Jobs;

public sealed class ExecutionLogRetentionService : IExecutionLogRetentionService
{
    private readonly ArchiveDbContext _dbContext;
    private readonly IArchiveApplicationSettingsService _settingsService;

    public ExecutionLogRetentionService(
        ArchiveDbContext dbContext,
        IArchiveApplicationSettingsService settingsService)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
    }

    public async Task PruneAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(false);

        if (settings.LogRetentionValue <= 0)
        {
            return;
        }

        var cutoffUtc = settings.LogRetentionUnit == LogRetentionUnit.Months
            ? DateTime.UtcNow.AddMonths(-settings.LogRetentionValue)
            : DateTime.UtcNow.AddDays(-settings.LogRetentionValue);

        var oldLogs = await _dbContext.ExecutionLogs
            .Where(x => x.Timestamp < cutoffUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (oldLogs.Count == 0)
        {
            return;
        }

        _dbContext.ExecutionLogs.RemoveRange(oldLogs);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}