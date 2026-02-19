namespace Archive.Core.Configuration;

public interface IArchiveApplicationSettingsService
{
    Task<ArchiveApplicationSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(ArchiveApplicationSettings settings, CancellationToken cancellationToken = default);
}