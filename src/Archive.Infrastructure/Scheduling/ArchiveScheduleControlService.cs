using Archive.Core.Configuration;
using Archive.Core.Domain.Entities;
using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Archive.Infrastructure.Scheduling;

public sealed class ArchiveScheduleControlService : IArchiveScheduleControlService
{
    private const string ScheduleEnabledKey = "ArchiveScheduleEnabled";
    private const int MaxRetryAttempts = 5;

    private readonly ArchiveDbContext _dbContext;
    private readonly IScheduler _scheduler;
    private readonly AppSettings _appSettings;
    private readonly ILogger<ArchiveScheduleControlService> _logger;

    public ArchiveScheduleControlService(
        ArchiveDbContext dbContext,
        IScheduler scheduler,
        AppSettings appSettings,
        ILogger<ArchiveScheduleControlService>? logger = null)
    {
        _dbContext = dbContext;
        _scheduler = scheduler;
        _appSettings = appSettings;
        _logger = logger ?? NullLogger<ArchiveScheduleControlService>.Instance;
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
            await RetryOnDatabaseLockAsync(
                static (scheduler, token) => scheduler.Start(token),
                _scheduler,
                "Start scheduler",
                cancellationToken);
        }

        if (enabled)
        {
            await RetryOnDatabaseLockAsync(
                static (scheduler, token) => scheduler.ResumeAll(token),
                _scheduler,
                "Resume scheduler",
                cancellationToken);
        }
        else
        {
            await RetryOnDatabaseLockAsync(
                static (scheduler, token) => scheduler.PauseAll(token),
                _scheduler,
                "Pause scheduler",
                cancellationToken);
        }
    }

    private async Task EnsureAppSettingsTableAsync(CancellationToken cancellationToken)
    {
        await RetryOnDatabaseLockAsync(
            static (dbContext, token) => dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL);",
                token),
            _dbContext,
            "Ensure AppSettings table",
            cancellationToken);
    }

    private async Task RetryOnDatabaseLockAsync<TState>(
        Func<TState, CancellationToken, Task> action,
        TState state,
        string operationName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await action(state, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsDatabaseLocked(ex) && attempt < MaxRetryAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Database lock during schedule control operation '{Operation}'. Retrying attempt {Attempt} of {MaxAttempts}.",
                    operationName,
                    attempt,
                    MaxRetryAttempts);

                await Task.Delay(250 * attempt, cancellationToken);
            }
        }

        await action(state, cancellationToken);
    }

    private static bool IsDatabaseLocked(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite && sqlite.SqliteErrorCode == 5)
            {
                return true;
            }

            if (current is JobPersistenceException && current.InnerException is null)
            {
                return current.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
                       || current.Message.Contains("Failure setting up connection", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
