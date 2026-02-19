namespace Archive.Core.Jobs;

public interface IExecutionLogRetentionService
{
    Task PruneAsync(CancellationToken cancellationToken = default);
}