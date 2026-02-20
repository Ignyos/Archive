using Archive.Core.Jobs;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Archive.Infrastructure.Scheduling;

public sealed class JobSchedulerService : IJobSchedulerService
{
    private readonly IScheduler _scheduler;
    private readonly ArchiveDbContext? _dbContext;
    private readonly IJobExecutionService? _executionService;

    public JobSchedulerService(IScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public JobSchedulerService(IScheduler scheduler, ArchiveDbContext dbContext)
    {
        _scheduler = scheduler;
        _dbContext = dbContext;
    }

    public JobSchedulerService(
        IScheduler scheduler,
        ArchiveDbContext dbContext,
        IJobExecutionService executionService)
    {
        _scheduler = scheduler;
        _dbContext = dbContext;
        _executionService = executionService;
    }

    public async Task ScheduleJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobKey = CreateJobKey(jobId);
        await _scheduler.DeleteJob(jobKey, cancellationToken);

        if (_dbContext is null)
        {
            var fallbackJobDetail = CreateJobDetail(jobKey, jobId);
            var fallbackTrigger = TriggerBuilder.Create()
                .WithIdentity($"archive-trigger-{jobId}")
                .StartNow()
                .Build();

            await _scheduler.ScheduleJob(fallbackJobDetail, fallbackTrigger, cancellationToken);
            return;
        }

        var job = await _dbContext.BackupJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId && x.DeletedAt == null, cancellationToken);

        if (job is null || !job.Enabled || job.TriggerType == TriggerType.Manual)
        {
            return;
        }

        var trigger = BuildTrigger(job);
        if (trigger is null)
        {
            return;
        }

        var jobDetail = CreateJobDetail(jobKey, jobId);
        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
    }

    public async Task RunNowAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_executionService is not null)
        {
            _ = RunDirectExecutionInBackgroundAsync(jobId);
            return;
        }

        try
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
        catch when (_executionService is not null)
        {
            _ = RunDirectExecutionInBackgroundAsync(jobId);
        }
    }

    private async Task RunDirectExecutionInBackgroundAsync(Guid jobId)
    {
        if (_executionService is null)
        {
            return;
        }

        try
        {
            await _executionService.ExecuteAsync(jobId, CancellationToken.None);
        }
        catch
        {
        }
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

    private static ITrigger? BuildTrigger(BackupJob job)
    {
        var triggerIdentity = $"archive-trigger-{job.Id}";
        var jobKey = CreateJobKey(job.Id);

        switch (job.TriggerType)
        {
            case TriggerType.Recurring:
            {
                if (string.IsNullOrWhiteSpace(job.CronExpression) || !CronExpression.IsValidExpression(job.CronExpression))
                {
                    return null;
                }

                return TriggerBuilder.Create()
                    .WithIdentity(triggerIdentity)
                    .ForJob(jobKey)
                    .WithCronSchedule(job.CronExpression, cron => cron.WithMisfireHandlingInstructionDoNothing())
                    .Build();
            }

            case TriggerType.OneTime:
            {
                if (!job.SimpleTriggerTime.HasValue)
                {
                    return null;
                }

                var utcTime = job.SimpleTriggerTime.Value.Kind == DateTimeKind.Utc
                    ? job.SimpleTriggerTime.Value
                    : job.SimpleTriggerTime.Value.ToUniversalTime();

                var startAt = new DateTimeOffset(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc));
                if (startAt <= DateTimeOffset.UtcNow)
                {
                    return null;
                }

                return TriggerBuilder.Create()
                    .WithIdentity(triggerIdentity)
                    .ForJob(jobKey)
                    .StartAt(startAt)
                    .WithSimpleSchedule(x => x.WithRepeatCount(0))
                    .Build();
            }

            default:
                return null;
        }
    }
}
