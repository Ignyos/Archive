using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Core.Sync;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Jobs;

public sealed class JobExecutionService : IJobExecutionService
{
    private readonly ArchiveDbContext _dbContext;
    private readonly ISyncEngine _syncEngine;

    public JobExecutionService(ArchiveDbContext dbContext, ISyncEngine syncEngine)
    {
        _dbContext = dbContext;
        _syncEngine = syncEngine;
    }

    public async Task<JobExecution> ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BackupJobs
            .Include(x => x.SyncOptions)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            throw new InvalidOperationException($"BackupJob {jobId} not found.");
        }

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Status = JobExecutionStatus.Running,
            StartTime = DateTime.UtcNow
        };

        try
        {
            var result = await _syncEngine.ExecuteAsync(job, cancellationToken);

            execution.Status = JobExecutionStatus.Completed;
            execution.EndTime = DateTime.UtcNow;
            execution.Duration = execution.EndTime - execution.StartTime;
            execution.FilesScanned = result.FilesScanned;
            execution.FilesCopied = result.FilesCopied;
            execution.FilesUpdated = result.FilesUpdated;
            execution.FilesDeleted = result.FilesDeleted;
            execution.FilesSkipped = result.FilesSkipped;
            execution.FilesFailed = result.FilesFailed;
            execution.BytesTransferred = result.BytesTransferred;
            execution.ErrorCount = result.ErrorCount;
            execution.WarningCount = result.WarningCount;
        }
        catch (Exception ex)
        {
            execution.Status = JobExecutionStatus.Failed;
            execution.EndTime = DateTime.UtcNow;
            execution.Duration = execution.EndTime - execution.StartTime;
            execution.ErrorCount = 1;

            execution.Logs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(),
                JobExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Error,
                Message = ex.Message,
                ExceptionDetails = ex.ToString()
            });
        }

        _dbContext.JobExecutions.Add(execution);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return execution;
    }
}
