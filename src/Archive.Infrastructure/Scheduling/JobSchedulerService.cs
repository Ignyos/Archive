using Archive.Core.Jobs;
using Quartz;

namespace Archive.Infrastructure.Scheduling;

public sealed class JobSchedulerService : IJobSchedulerService
{
    private readonly IScheduler _scheduler;
    private readonly IJobExecutionService _executionService;

    public JobSchedulerService(IScheduler scheduler, IJobExecutionService executionService)
    {
        _scheduler = scheduler;
        _executionService = executionService;
    }

    public async Task ScheduleJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = new JobKey($"archive-job-{jobId}");
        var jobDetail = JobBuilder.Create<ArchiveJob>()
            .WithIdentity(jobKey)
            .UsingJobData(ArchiveJob.JobIdKey, jobId.ToString())
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"archive-trigger-{jobId}")
            .StartNow()
            .Build();

        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
    }

    public Task RunNowAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return _executionService.ExecuteAsync(jobId, cancellationToken);
    }
}
