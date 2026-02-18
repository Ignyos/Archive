using Archive.Core.Jobs;
using Quartz;

namespace Archive.Infrastructure.Scheduling;

public sealed class JobSchedulerService : IJobSchedulerService
{
    private readonly IScheduler _scheduler;

    public JobSchedulerService(IScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public async Task ScheduleJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = CreateJobKey(jobId);
        var jobDetail = CreateJobDetail(jobKey, jobId);

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"archive-trigger-{jobId}")
            .StartNow()
            .Build();

        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
    }

    public async Task RunNowAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = CreateJobKey(jobId);

        var exists = await _scheduler.CheckExists(jobKey, cancellationToken);
        if (!exists)
        {
            var jobDetail = CreateJobDetail(jobKey, jobId);
            var trigger = TriggerBuilder.Create()
                .WithIdentity($"archive-run-now-{jobId}-{Guid.NewGuid():N}")
                .ForJob(jobKey)
                .StartNow()
                .Build();

            await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
            return;
        }

        await _scheduler.TriggerJob(jobKey, cancellationToken);
    }

    public Task<bool> StopAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = CreateJobKey(jobId);
        return _scheduler.Interrupt(jobKey, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = CreateJobKey(jobId);
        return _scheduler.DeleteJob(jobKey, cancellationToken);
    }

    private static JobKey CreateJobKey(Guid jobId)
    {
        return new JobKey($"archive-job-{jobId}");
    }

    private static IJobDetail CreateJobDetail(JobKey jobKey, Guid jobId)
    {
        return JobBuilder.Create<ArchiveJob>()
            .WithIdentity(jobKey)
            .UsingJobData(ArchiveJob.JobIdKey, jobId.ToString())
            .Build();
    }
}
