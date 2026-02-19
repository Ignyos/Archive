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
    private readonly IExecutionLogRetentionService? _retentionService;

    public JobExecutionService(ArchiveDbContext dbContext, ISyncEngine syncEngine)
    {
        _dbContext = dbContext;
        _syncEngine = syncEngine;
    }

    public JobExecutionService(
        ArchiveDbContext dbContext,
        ISyncEngine syncEngine,
        IExecutionLogRetentionService retentionService)
    {
        _dbContext = dbContext;
        _syncEngine = syncEngine;
        _retentionService = retentionService;
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

        JobExecutionNotificationHub.Publish(new JobExecutionNotificationEvent
        {
            JobId = job.Id,
            JobName = string.IsNullOrWhiteSpace(job.Name) ? "(unnamed)" : job.Name,
            Kind = JobExecutionNotificationKind.Started,
            NotifyOnStartOverride = job.NotifyOnStart,
            NotifyOnCompleteOverride = job.NotifyOnComplete,
            NotifyOnFailOverride = job.NotifyOnFail
        });

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

            execution.Status = result.WarningCount > 0 || result.ErrorCount > 0 || result.FilesFailed > 0
                ? JobExecutionStatus.CompletedWithWarnings
                : JobExecutionStatus.Completed;
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

        var completionKind = execution.Status is JobExecutionStatus.Failed or JobExecutionStatus.CompletedWithWarnings
            ? JobExecutionNotificationKind.Failed
            : JobExecutionNotificationKind.Completed;

        var detailSummary = await BuildDetailSummaryAsync(execution.Id, cancellationToken);

        JobExecutionNotificationHub.Publish(new JobExecutionNotificationEvent
        {
            JobId = job.Id,
            JobName = string.IsNullOrWhiteSpace(job.Name) ? "(unnamed)" : job.Name,
            Kind = completionKind,
            Status = execution.Status,
            WarningCount = execution.WarningCount,
            ErrorCount = execution.ErrorCount,
            FilesFailed = execution.FilesFailed,
            DetailSummary = detailSummary,
            NotifyOnStartOverride = job.NotifyOnStart,
            NotifyOnCompleteOverride = job.NotifyOnComplete,
            NotifyOnFailOverride = job.NotifyOnFail
        });

        if (_retentionService is not null)
        {
            try
            {
                await _retentionService.PruneAsync(cancellationToken);
            }
            catch
            {
            }
        }

        return execution;
    }

    private async Task<string?> BuildDetailSummaryAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var logs = await _dbContext.ExecutionLogs
            .AsNoTracking()
            .Where(x => x.JobExecutionId == executionId && (x.Level == LogLevel.Error || x.Level == LogLevel.Warning))
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new
            {
                x.Level,
                x.OperationType,
                x.Message
            })
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return null;
        }

        var errorOps = logs
            .Where(x => x.Level == LogLevel.Error && x.OperationType.HasValue)
            .Select(x => x.OperationType!.Value.ToString())
            .Distinct()
            .ToList();

        var warningOps = logs
            .Where(x => x.Level == LogLevel.Warning && x.OperationType.HasValue)
            .Select(x => x.OperationType!.Value.ToString())
            .Distinct()
            .ToList();

        var latestMessage = logs
            .Select(x => x.Message)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var fragments = new List<string>();

        if (errorOps.Count > 0)
        {
            fragments.Add($"Error operations: {string.Join(", ", errorOps)}");
        }

        if (warningOps.Count > 0)
        {
            fragments.Add($"Warning operations: {string.Join(", ", warningOps)}");
        }

        if (!string.IsNullOrWhiteSpace(latestMessage))
        {
            fragments.Add($"Latest issue: {latestMessage}");
        }

        return fragments.Count == 0
            ? null
            : string.Join(" | ", fragments);
    }
}
