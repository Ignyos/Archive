using Archive.Core.Configuration;
using Archive.Core.Domain.Entities;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Configuration;

public sealed class ArchiveApplicationSettingsService : IArchiveApplicationSettingsService
{
    private const string RunOnWindowsStartupKey = "RunOnWindowsStartup";
    private const string EnableNotificationsKey = "EnableNotifications";
    private const string NotifyOnStartKey = "NotifyOnStart";
    private const string NotifyOnCompleteKey = "NotifyOnComplete";
    private const string NotifyOnFailKey = "NotifyOnFail";
    private const string PlayNotificationSoundKey = "PlayNotificationSound";
    private const string LogRetentionValueKey = "LogRetentionValue";
    private const string LogRetentionUnitKey = "LogRetentionUnit";
    private const string EnableVerboseLoggingKey = "EnableVerboseLogging";

    private readonly ArchiveDbContext _dbContext;

    public ArchiveApplicationSettingsService(ArchiveDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ArchiveApplicationSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.AppSettings
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken)
            .ConfigureAwait(false);

        return new ArchiveApplicationSettings
        {
            RunOnWindowsStartup = TryGetBool(rows, RunOnWindowsStartupKey, defaultValue: false),
            EnableNotifications = TryGetBool(rows, EnableNotificationsKey, defaultValue: true),
            NotifyOnStart = TryGetBool(rows, NotifyOnStartKey, defaultValue: false),
            NotifyOnComplete = TryGetBool(rows, NotifyOnCompleteKey, defaultValue: true),
            NotifyOnFail = TryGetBool(rows, NotifyOnFailKey, defaultValue: true),
            PlayNotificationSound = TryGetBool(rows, PlayNotificationSoundKey, defaultValue: true),
            LogRetentionValue = TryGetInt(rows, LogRetentionValueKey, defaultValue: 7),
            LogRetentionUnit = TryGetEnum(rows, LogRetentionUnitKey, defaultValue: LogRetentionUnit.Days),
            EnableVerboseLogging = TryGetBool(rows, EnableVerboseLoggingKey, defaultValue: false)
        };
    }

    public async Task SetAsync(ArchiveApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        await UpsertAsync(RunOnWindowsStartupKey, settings.RunOnWindowsStartup.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(EnableNotificationsKey, settings.EnableNotifications.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(NotifyOnStartKey, settings.NotifyOnStart.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(NotifyOnCompleteKey, settings.NotifyOnComplete.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(NotifyOnFailKey, settings.NotifyOnFail.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(PlayNotificationSoundKey, settings.PlayNotificationSound.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(LogRetentionValueKey, settings.LogRetentionValue.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(LogRetentionUnitKey, settings.LogRetentionUnit.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(EnableVerboseLoggingKey, settings.EnableVerboseLogging.ToString(), cancellationToken).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
    {
        var row = await _dbContext.AppSettings
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            _dbContext.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value
            });

            return;
        }

        row.Value = value;
    }

    private static bool TryGetBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int TryGetInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
    {
        return values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) && parsed >= 0
            ? parsed
            : defaultValue;
    }

    private static TEnum TryGetEnum<TEnum>(IReadOnlyDictionary<string, string> values, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        return values.TryGetValue(key, out var raw) && Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;
    }
}