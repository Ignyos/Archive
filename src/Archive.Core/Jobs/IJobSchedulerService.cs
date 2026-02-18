namespace Archive.Core.Jobs;

public interface IJobSchedulerService
{
    Task ScheduleJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task RunNowAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<bool> StopAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
}
