using Archive.Core.Domain.Enums;

namespace Archive.Core.Jobs;

public interface IBackupJobStateService
{
    Task<bool> SetEnabledAsync(Guid jobId, bool enabled, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<bool> UpdateBasicFieldsAsync(
        Guid jobId,
        string name,
        string? description,
        string sourcePath,
        string destinationPath,
        bool enabled,
        TriggerType triggerType,
        string? cronExpression,
        DateTime? simpleTriggerTime,
        bool recursive = true,
        bool deleteOrphaned = false,
        bool skipHiddenAndSystem = true,
        bool verifyAfterCopy = false,
        CancellationToken cancellationToken = default);
}