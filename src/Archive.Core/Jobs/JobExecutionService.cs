using Archive.Core.Domain.Entities;

namespace Archive.Core.Jobs;

public interface IJobExecutionService
{
    Task<JobExecution> ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default);
}
