namespace Archive.Core.Jobs;

public interface IArchiveScheduleControlService
{
    Task<bool> GetScheduleEnabledAsync(CancellationToken cancellationToken = default);

    Task SetScheduleEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task InitializeAsync(CancellationToken cancellationToken = default);
}
