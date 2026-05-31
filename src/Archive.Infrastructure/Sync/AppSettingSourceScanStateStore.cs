using System.Text.Json;
using Archive.Core.Domain.Entities;
using Archive.Core.Sync;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Sync;

public sealed class AppSettingSourceScanStateStore : ISourceScanStateStore
{
    private const string KeyPrefix = "Sync.SourceScanState.";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ArchiveDbContext _dbContext;

    public AppSettingSourceScanStateStore(ArchiveDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SourceScanState?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(jobId);
        var appSetting = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (appSetting is null || string.IsNullOrWhiteSpace(appSetting.Value))
        {
            return null;
        }

        var persisted = JsonSerializer.Deserialize<PersistedSourceScanState>(appSetting.Value, SerializerOptions);
        if (persisted is null)
        {
            return null;
        }

        return new SourceScanState
        {
            JobId = jobId,
            Fingerprint = persisted.Fingerprint ?? string.Empty,
            FileCount = persisted.FileCount,
            TotalBytes = persisted.TotalBytes,
            UpdatedAtUtc = persisted.UpdatedAtUtc
        };
    }

    public async Task SaveAsync(SourceScanState state, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(state.JobId);
        var persisted = new PersistedSourceScanState
        {
            Fingerprint = state.Fingerprint,
            FileCount = state.FileCount,
            TotalBytes = state.TotalBytes,
            UpdatedAtUtc = state.UpdatedAtUtc
        };

        var payload = JsonSerializer.Serialize(persisted, SerializerOptions);

        var appSetting = await _dbContext.AppSettings
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (appSetting is null)
        {
            _dbContext.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = payload
            });
        }
        else
        {
            appSetting.Value = payload;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildKey(Guid jobId)
    {
        return KeyPrefix + jobId.ToString("N");
    }

    private sealed class PersistedSourceScanState
    {
        public string? Fingerprint { get; init; }

        public int FileCount { get; init; }

        public long TotalBytes { get; init; }

        public DateTime UpdatedAtUtc { get; init; }
    }
}
