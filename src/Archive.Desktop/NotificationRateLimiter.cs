using Archive.Core.Jobs;

namespace Archive.Desktop;

public sealed class NotificationRateLimiter
{
    private readonly TimeSpan _minimumInterval;
    private readonly TimeSpan _dedupeWindow;
    private readonly Dictionary<string, DateTime> _lastShownByKey = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _lastShownAtUtc;

    public NotificationRateLimiter(TimeSpan? minimumInterval = null, TimeSpan? dedupeWindow = null)
    {
        _minimumInterval = minimumInterval ?? TimeSpan.FromSeconds(2);
        _dedupeWindow = dedupeWindow ?? TimeSpan.FromSeconds(20);
    }

    public bool ShouldShow(JobExecutionNotificationEvent notificationEvent, DateTime nowUtc)
    {
        var normalizedNow = nowUtc.Kind == DateTimeKind.Utc
            ? nowUtc
            : nowUtc.ToUniversalTime();

        PruneOldEntries(normalizedNow);

        if (_lastShownAtUtc.HasValue && normalizedNow - _lastShownAtUtc.Value < _minimumInterval)
        {
            return false;
        }

        var key = BuildKey(notificationEvent);
        if (_lastShownByKey.TryGetValue(key, out var lastForKey)
            && normalizedNow - lastForKey < _dedupeWindow)
        {
            return false;
        }

        _lastShownByKey[key] = normalizedNow;
        _lastShownAtUtc = normalizedNow;
        return true;
    }

    private void PruneOldEntries(DateTime nowUtc)
    {
        var staleKeys = _lastShownByKey
            .Where(x => nowUtc - x.Value >= _dedupeWindow)
            .Select(x => x.Key)
            .ToList();

        foreach (var staleKey in staleKeys)
        {
            _lastShownByKey.Remove(staleKey);
        }
    }

    private static string BuildKey(JobExecutionNotificationEvent notificationEvent)
    {
        var status = notificationEvent.Status?.ToString() ?? string.Empty;
        var summary = notificationEvent.DetailSummary ?? string.Empty;
        return $"{notificationEvent.JobId:N}|{notificationEvent.Kind}|{status}|{summary}";
    }
}