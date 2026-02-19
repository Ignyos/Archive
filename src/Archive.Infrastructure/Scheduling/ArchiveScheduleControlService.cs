using Archive.Core.Configuration;
using Archive.Core.Domain.Entities;
using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Archive.Infrastructure.Scheduling;

public sealed class ArchiveScheduleControlService : IArchiveScheduleControlService
{
    private const string ScheduleEnabledKey = "ArchiveScheduleEnabled";

    private readonly ArchiveDbContext _dbContext;
    private readonly IScheduler _scheduler;
    private readonly AppSettings _appSettings;

    public ArchiveScheduleControlService(
        ArchiveDbContext dbContext,
        IScheduler scheduler,
        AppSettings appSettings)
    {
        _dbContext = dbContext;
        _scheduler = scheduler;
        _appSettings = appSettings;
    }

    public async Task<bool> GetScheduleEnabledAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAppSettingsTableAsync(cancellationToken);

        var row = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == ScheduleEnabledKey, cancellationToken);

        if (row is null)
        {
            return _appSettings.Archive.ArchiveScheduleEnabled;
        }

        if (bool.TryParse(row.Value, out var parsed))
        {
            return parsed;
        }

        return _appSettings.Archive.ArchiveScheduleEnabled;
    }

    public async Task SetScheduleEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await EnsureAppSettingsTableAsync(cancellationToken);

        var row = await _dbContext.AppSettings
            .FirstOrDefaultAsync(x => x.Key == ScheduleEnabledKey, cancellationToken);

        if (row is null)
        {
            row = new AppSetting
            {
                Key = ScheduleEnabledKey,
                Value = enabled.ToString()
            };
            _dbContext.AppSettings.Add(row);
        }
        else
        {
            row.Value = enabled.ToString();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await ApplySchedulerStateAsync(enabled, cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAppSettingsTableAsync(cancellationToken);

        var enabled = await GetScheduleEnabledAsync(cancellationToken);

        var row = await _dbContext.AppSettings
            .FirstOrDefaultAsync(x => x.Key == ScheduleEnabledKey, cancellationToken);

        if (row is null)
        {
            _dbContext.AppSettings.Add(new AppSetting
            {
                Key = ScheduleEnabledKey,
                Value = enabled.ToString()
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await ApplySchedulerStateAsync(enabled, cancellationToken);
    }

    private async Task ApplySchedulerStateAsync(bool enabled, CancellationToken cancellationToken)
    {
        if (!_scheduler.IsStarted)
        {
            await _scheduler.Start(cancellationToken);
        }

        if (enabled)
        {
            await _scheduler.ResumeAll(cancellationToken);
        }
        else
        {
            await _scheduler.PauseAll(cancellationToken);
        }
    }

    private Task EnsureAppSettingsTableAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL);",
            cancellationToken);
    }
}
