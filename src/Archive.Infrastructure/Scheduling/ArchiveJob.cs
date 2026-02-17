using Archive.Core.Jobs;
using Quartz;

namespace Archive.Infrastructure.Scheduling;

public sealed class ArchiveJob : IJob
{
    public const string JobIdKey = "JobId";

    private readonly IJobExecutionService _executionService;

    public ArchiveJob(IJobExecutionService executionService)
    {
        _executionService = executionService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!context.MergedJobDataMap.ContainsKey(JobIdKey))
        {
            throw new InvalidOperationException("JobId missing from JobDataMap.");
        }

        var jobId = Guid.Parse(context.MergedJobDataMap.GetString(JobIdKey)!);
        await _executionService.ExecuteAsync(jobId, context.CancellationToken);
    }
}
