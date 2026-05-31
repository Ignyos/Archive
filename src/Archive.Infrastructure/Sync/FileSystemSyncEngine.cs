using System.Security.Cryptography;
using System.Text;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Core.Sync;

namespace Archive.Infrastructure.Sync;

public sealed class FileSystemSyncEngine : ISyncEngine
{
    private readonly SyncDecisionService _decisionService = new();
    private readonly ISourceScanStateStore? _sourceScanStateStore;

    public FileSystemSyncEngine()
    {
    }

    public FileSystemSyncEngine(ISourceScanStateStore sourceScanStateStore)
    {
        _sourceScanStateStore = sourceScanStateStore;
    }

    public async Task<SyncResult> ExecuteAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        var operationLogs = new List<SyncOperationLog>();
        var filesScanned = 0;
        var filesCopied = 0;
        var filesUpdated = 0;
        var filesDeleted = 0;
        var filesSkipped = 0;
        var filesFailed = 0;
        var bytesTransferred = 0L;
        var errorCount = 0;
        var warningCount = 0;
        var exclusionPatterns = IgnoreRuleMatcher.NormalizeRules((job.BackupJobExclusionPatterns ?? Array.Empty<BackupJobExclusionPattern>())
            .Select(x => x.ExclusionPattern?.Pattern));

        if (File.Exists(job.SourcePath))
        {
            return await ExecuteSingleFileAsync(job, exclusionPatterns, cancellationToken);
        }

        if (!Directory.Exists(job.SourcePath))
        {
            throw new DirectoryNotFoundException($"Source path not found: {job.SourcePath}");
        }

        Directory.CreateDirectory(job.DestinationPath);

        var recursive = job.SyncOptions?.Recursive ?? true;
        var skipHiddenAndSystem = job.SyncOptions?.SkipHiddenAndSystem ?? true;
        var shouldDeleteOrphans = job.SyncMode == SyncMode.Mirror || (job.SyncOptions?.DeleteOrphaned ?? false);
        var shouldReportManualProgress = job.TriggerType == TriggerType.Manual;
        var skipNoChangeOptimization = shouldDeleteOrphans;

        var sourceScanSnapshot = BuildSourceScanSnapshot(
            job,
            recursive,
            skipHiddenAndSystem,
            exclusionPatterns,
            cancellationToken);

        operationLogs.AddRange(sourceScanSnapshot.InitialLogs);
        filesScanned = sourceScanSnapshot.FilesScanned;
        filesSkipped = sourceScanSnapshot.FilesSkipped;

        if (!skipNoChangeOptimization && _sourceScanStateStore is not null)
        {
            try
            {
                var previousState = await _sourceScanStateStore.GetAsync(job.Id, cancellationToken);
                if (previousState is not null
                    && string.Equals(previousState.Fingerprint, sourceScanSnapshot.Fingerprint, StringComparison.Ordinal))
                {
                    operationLogs.Add(new SyncOperationLog(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        "No source changes detected since last successful sync. Skipping transfer scan.",
                        job.SourcePath,
                        OperationType.Skip));

                    return new SyncResult
                    {
                        FilesScanned = filesScanned,
                        FilesCopied = 0,
                        FilesUpdated = 0,
                        FilesDeleted = 0,
                        FilesSkipped = filesSkipped,
                        FilesFailed = 0,
                        BytesTransferred = 0,
                        ErrorCount = 0,
                        WarningCount = warningCount,
                        OperationLogs = operationLogs
                    };
                }
            }
            catch (Exception ex)
            {
                warningCount++;
                operationLogs.Add(new SyncOperationLog(
                    DateTime.UtcNow,
                    LogLevel.Warning,
                    $"Unable to read previous source scan state. Continuing with full scan. {ex.Message}",
                    job.SourcePath,
                    OperationType.Skip,
                    ex.ToString()));
            }
        }

        var plan = BuildSyncPlan(job, sourceScanSnapshot.Candidates, cancellationToken);
        operationLogs.AddRange(plan.InitialLogs);
        filesSkipped += plan.FilesSkipped;

        var plannedTransferBytes = plan.PlannedTransferBytes;
        var completedTransferBytes = 0L;

        if (shouldReportManualProgress)
        {
            PublishProgress(job, plannedTransferBytes, completedTransferBytes);
        }

        foreach (var plannedOperation in plan.AffectedOperations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationFullPath = plannedOperation.DestinationFullPath;
            var destinationDirectory = Path.GetDirectoryName(destinationFullPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var sourceFullPath = plannedOperation.SourceFullPath;
            var sourceInfo = plannedOperation.SourceInfo;
            var action = plannedOperation.Action;

            try
            {
                switch (action)
                {
                    case SyncAction.Copy:
                        await CopyFileAsync(sourceFullPath, destinationFullPath, overwrite: true, cancellationToken);
                        filesCopied++;
                        bytesTransferred += sourceInfo.Length;
                        if (shouldReportManualProgress)
                        {
                            completedTransferBytes += sourceInfo.Length;
                            PublishProgress(job, plannedTransferBytes, completedTransferBytes);
                        }
                        operationLogs.Add(new SyncOperationLog(
                            DateTime.UtcNow,
                            LogLevel.Info,
                            "Copied file.",
                            sourceFullPath,
                            OperationType.Copy));
                        break;

                    case SyncAction.Update:
                        if (job.OverwriteBehavior == OverwriteBehavior.KeepBoth && File.Exists(destinationFullPath))
                        {
                            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
                            var extension = Path.GetExtension(destinationFullPath);
                            var basePath = destinationFullPath[..^extension.Length];
                            destinationFullPath = $"{basePath}_{timestamp}{extension}";
                        }

                        await CopyFileAsync(sourceFullPath, destinationFullPath, overwrite: true, cancellationToken);
                        filesUpdated++;
                        bytesTransferred += sourceInfo.Length;
                        if (shouldReportManualProgress)
                        {
                            completedTransferBytes += sourceInfo.Length;
                            PublishProgress(job, plannedTransferBytes, completedTransferBytes);
                        }
                        operationLogs.Add(new SyncOperationLog(
                            DateTime.UtcNow,
                            LogLevel.Info,
                            "Updated file.",
                            sourceFullPath,
                            OperationType.Update));
                        break;

                    case SyncAction.Skip:
                        break;
                }

                if (job.SyncOptions?.VerifyAfterCopy == true && action is SyncAction.Copy or SyncAction.Update)
                {
                    var verified = await VerifyFileContentAsync(sourceFullPath, destinationFullPath, cancellationToken);
                    if (!verified)
                    {
                        warningCount++;
                        filesFailed++;
                        errorCount++;
                        operationLogs.Add(new SyncOperationLog(
                            DateTime.UtcNow,
                            LogLevel.Warning,
                            "Verification failed after copy/update.",
                            sourceFullPath,
                            action == SyncAction.Copy ? OperationType.Copy : OperationType.Update));
                    }
                }
            }
            catch (Exception ex)
            {
                filesFailed++;
                errorCount++;
                operationLogs.Add(new SyncOperationLog(
                    DateTime.UtcNow,
                    LogLevel.Error,
                    ex.Message,
                    sourceFullPath,
                    action == SyncAction.Copy
                        ? OperationType.Copy
                        : action == SyncAction.Update
                            ? OperationType.Update
                            : OperationType.Skip,
                    ex.ToString()));
            }
        }

        if (shouldDeleteOrphans)
        {
            var destinationFiles = EnumerateFilesSafe(job.DestinationPath, recursive);

            foreach (var destinationFullPath in destinationFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destinationRelativePath = Path.GetRelativePath(job.DestinationPath, destinationFullPath);

                if (IgnoreRuleMatcher.IsIgnored(destinationRelativePath, exclusionPatterns))
                {
                    continue;
                }

                if (plan.SourceRelativeToFullPath.ContainsKey(destinationRelativePath))
                {
                    continue;
                }

                try
                {
                    File.Delete(destinationFullPath);
                    filesDeleted++;
                    operationLogs.Add(new SyncOperationLog(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        "Deleted orphaned destination file.",
                        destinationFullPath,
                        OperationType.Delete));
                }
                catch (Exception ex)
                {
                    filesFailed++;
                    errorCount++;
                    operationLogs.Add(new SyncOperationLog(
                        DateTime.UtcNow,
                        LogLevel.Error,
                        ex.Message,
                        destinationFullPath,
                        OperationType.Delete,
                        ex.ToString()));
                }
            }
        }

        if (_sourceScanStateStore is not null)
        {
            try
            {
                await _sourceScanStateStore.SaveAsync(new SourceScanState
                {
                    JobId = job.Id,
                    Fingerprint = sourceScanSnapshot.Fingerprint,
                    FileCount = sourceScanSnapshot.FilesScanned,
                    TotalBytes = sourceScanSnapshot.TotalBytes,
                    UpdatedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                warningCount++;
                operationLogs.Add(new SyncOperationLog(
                    DateTime.UtcNow,
                    LogLevel.Warning,
                    $"Unable to persist source scan state. {ex.Message}",
                    job.SourcePath,
                    OperationType.Skip,
                    ex.ToString()));
            }
        }

        return new SyncResult
        {
            FilesScanned = filesScanned,
            FilesCopied = filesCopied,
            FilesUpdated = filesUpdated,
            FilesDeleted = filesDeleted,
            FilesSkipped = filesSkipped,
            FilesFailed = filesFailed,
            BytesTransferred = bytesTransferred,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            OperationLogs = operationLogs
        };
    }

    private SyncPlan BuildSyncPlan(
        BackupJob job,
        IReadOnlyList<SourceFileCandidate> sourceCandidates,
        CancellationToken cancellationToken)
    {
        var initialLogs = new List<SyncOperationLog>();
        var affectedOperations = new List<PlannedFileOperation>();
        var sourceRelativeToFullPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var filesSkipped = 0;
        var plannedTransferBytes = 0L;

        foreach (var candidate in sourceCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceFullPath = candidate.SourceFullPath;
            var sourceRelativePath = candidate.SourceRelativePath;
            var sourceInfo = candidate.SourceInfo;
            var sourceSnapshot = new FileSnapshot(sourceFullPath, sourceInfo.Length, sourceInfo.LastWriteTimeUtc);
            var destinationFullPath = Path.Combine(job.DestinationPath, sourceRelativePath);

            FileSnapshot? destinationSnapshot = null;
            if (File.Exists(destinationFullPath))
            {
                var destinationInfo = new FileInfo(destinationFullPath);
                destinationSnapshot = new FileSnapshot(
                    destinationFullPath,
                    destinationInfo.Length,
                    destinationInfo.LastWriteTimeUtc);
            }

            var action = _decisionService.Decide(
                sourceSnapshot,
                destinationSnapshot,
                job.SyncMode,
                job.ComparisonMethod,
                job.OverwriteBehavior);

            sourceRelativeToFullPath[sourceRelativePath] = sourceFullPath;

            if (action == SyncAction.Skip)
            {
                filesSkipped++;
                initialLogs.Add(new SyncOperationLog(
                    DateTime.UtcNow,
                    LogLevel.Info,
                    "Skipped unchanged file.",
                    sourceFullPath,
                    OperationType.Skip));
                continue;
            }

            affectedOperations.Add(new PlannedFileOperation(
                sourceFullPath,
                destinationFullPath,
                sourceInfo,
                action));

            plannedTransferBytes += sourceInfo.Length;
        }

        return new SyncPlan(
            sourceRelativeToFullPath,
            affectedOperations,
            initialLogs,
            filesSkipped,
            plannedTransferBytes);
    }

    private SourceScanSnapshot BuildSourceScanSnapshot(
        BackupJob job,
        bool recursive,
        bool skipHiddenAndSystem,
        IReadOnlyList<string> exclusionPatterns,
        CancellationToken cancellationToken)
    {
        var initialLogs = new List<SyncOperationLog>();
        var candidates = new List<SourceFileCandidate>();
        var filesScanned = 0;
        var filesSkipped = 0;
        var totalBytes = 0L;

        foreach (var sourceFullPath in EnumerateFilesSafe(job.SourcePath, recursive))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceRelativePath = Path.GetRelativePath(job.SourcePath, sourceFullPath);

            if (IgnoreRuleMatcher.IsIgnored(sourceRelativePath, exclusionPatterns))
            {
                filesSkipped++;
                initialLogs.Add(new SyncOperationLog(
                    DateTime.UtcNow,
                    LogLevel.Info,
                    "Skipped excluded file.",
                    sourceFullPath,
                    OperationType.Skip));
                continue;
            }

            if (skipHiddenAndSystem && IsHiddenOrSystem(sourceFullPath))
            {
                filesSkipped++;
                initialLogs.Add(new SyncOperationLog(
                    DateTime.UtcNow,
                    LogLevel.Info,
                    "Skipped hidden or system file.",
                    sourceFullPath,
                    OperationType.Skip));
                continue;
            }

            var sourceInfo = new FileInfo(sourceFullPath);
            candidates.Add(new SourceFileCandidate(sourceFullPath, sourceRelativePath, sourceInfo));
            filesScanned++;
            totalBytes += sourceInfo.Length;
        }

        var fingerprint = ComputeSourceFingerprint(
            job.SourcePath,
            recursive,
            skipHiddenAndSystem,
            exclusionPatterns,
            candidates);

        return new SourceScanSnapshot(
            candidates,
            initialLogs,
            filesScanned,
            filesSkipped,
            totalBytes,
            fingerprint);
    }

    private static string ComputeSourceFingerprint(
        string sourcePath,
        bool recursive,
        bool skipHiddenAndSystem,
        IReadOnlyList<string> exclusionPatterns,
        IReadOnlyList<SourceFileCandidate> candidates)
    {
        using var hasher = SHA256.Create();

        static void Append(HashAlgorithm hasher, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        Append(hasher, "v1\n");
        Append(hasher, $"source:{NormalizePathForFingerprint(sourcePath)}\n");
        Append(hasher, $"recursive:{recursive}\n");
        Append(hasher, $"skipHiddenAndSystem:{skipHiddenAndSystem}\n");

        foreach (var pattern in exclusionPatterns.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Append(hasher, $"rule:{pattern.Trim().ToLowerInvariant()}\n");
        }

        foreach (var candidate in candidates.OrderBy(x => x.SourceRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var normalizedRelativePath = candidate.SourceRelativePath.Replace('\\', '/').ToLowerInvariant();
            Append(hasher, $"file:{normalizedRelativePath}|{candidate.SourceInfo.Length}|{candidate.SourceInfo.LastWriteTimeUtc.Ticks}\n");
        }

        hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(hasher.Hash!);
    }

    private static string NormalizePathForFingerprint(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim())
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
        }
        catch
        {
            return path.Trim()
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
        }
    }

    private static async Task<SyncResult> ExecuteSingleFileAsync(
        BackupJob job,
        IReadOnlyList<string> exclusionPatterns,
        CancellationToken cancellationToken)
    {
        var sourceInfo = new FileInfo(job.SourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("Source file not found.", job.SourcePath);
        }

        if (IgnoreRuleMatcher.IsIgnored(sourceInfo.Name, exclusionPatterns))
        {
            return new SyncResult
            {
                FilesScanned = 1,
                FilesSkipped = 1,
                OperationLogs =
                [
                    new SyncOperationLog(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        "Skipped excluded file.",
                        sourceInfo.FullName,
                        OperationType.Skip)
                ]
            };
        }

        var shouldReportManualProgress = job.TriggerType == TriggerType.Manual;
        if (shouldReportManualProgress)
        {
            PublishProgress(job, sourceInfo.Length, 0);
        }

        Directory.CreateDirectory(job.DestinationPath);
        var destinationFullPath = Path.Combine(job.DestinationPath, sourceInfo.Name);

        try
        {
            await CopyFileAsync(job.SourcePath, destinationFullPath, overwrite: true, cancellationToken);
            if (shouldReportManualProgress)
            {
                PublishProgress(job, sourceInfo.Length, sourceInfo.Length);
            }

            return new SyncResult
            {
                FilesScanned = 1,
                FilesCopied = 1,
                BytesTransferred = sourceInfo.Length,
                OperationLogs =
                [
                    new SyncOperationLog(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        "Copied file.",
                        sourceInfo.FullName,
                        OperationType.Copy)
                ]
            };
        }
        catch (Exception ex)
        {
            return new SyncResult
            {
                FilesScanned = 1,
                FilesFailed = 1,
                ErrorCount = 1,
                OperationLogs =
                [
                    new SyncOperationLog(
                        DateTime.UtcNow,
                        LogLevel.Error,
                        ex.Message,
                        sourceInfo.FullName,
                        OperationType.Copy,
                        ex.ToString())
                ]
            };
        }
    }

    private static void PublishProgress(BackupJob job, long totalBytes, long completedBytes)
    {
        JobExecutionNotificationHub.Publish(new JobExecutionNotificationEvent
        {
            JobId = job.Id,
            JobName = string.IsNullOrWhiteSpace(job.Name) ? "(unnamed)" : job.Name,
            Kind = JobExecutionNotificationKind.Progress,
            IsManualRun = true,
            ProgressTotalBytes = totalBytes,
            ProgressCompletedBytes = completedBytes,
            NotifyOnStartOverride = job.NotifyOnStart,
            NotifyOnCompleteOverride = job.NotifyOnComplete,
            NotifyOnFailOverride = job.NotifyOnFail
        });
    }

    private sealed record PlannedFileOperation(
        string SourceFullPath,
        string DestinationFullPath,
        FileInfo SourceInfo,
        SyncAction Action);

    private sealed record SourceFileCandidate(
        string SourceFullPath,
        string SourceRelativePath,
        FileInfo SourceInfo);

    private sealed record SourceScanSnapshot(
        IReadOnlyList<SourceFileCandidate> Candidates,
        IReadOnlyList<SyncOperationLog> InitialLogs,
        int FilesScanned,
        int FilesSkipped,
        long TotalBytes,
        string Fingerprint);

    private sealed record SyncPlan(
        IReadOnlyDictionary<string, string> SourceRelativeToFullPath,
        IReadOnlyList<PlannedFileOperation> AffectedOperations,
        IReadOnlyList<SyncOperationLog> InitialLogs,
        int FilesSkipped,
        long PlannedTransferBytes);

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destinationStream = new FileStream(
            destinationPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        var sourceInfo = new FileInfo(sourcePath);
        var destinationInfo = new FileInfo(destinationPath);
        destinationInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
        destinationInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
        destinationInfo.Attributes = sourceInfo.Attributes;
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath, bool recursive)
    {
        if (!recursive)
        {
            IEnumerator<string>? topLevelFiles = null;
            try
            {
                topLevelFiles = Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly).GetEnumerator();
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }
            catch (IOException)
            {
                yield break;
            }

            using (topLevelFiles)
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = topLevelFiles.MoveNext();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        yield break;
                    }
                    catch (IOException)
                    {
                        yield break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    yield return topLevelFiles.Current;
                }
            }

            yield break;
        }

        var directories = new Stack<string>();
        directories.Push(rootPath);

        while (directories.Count > 0)
        {
            var currentDirectory = directories.Pop();

            IEnumerator<string>? files = null;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly).GetEnumerator();
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            if (files is not null)
            {
                using (files)
                {
                    while (true)
                    {
                        bool moved;
                        try
                        {
                            moved = files.MoveNext();
                        }
                        catch (UnauthorizedAccessException)
                        {
                            break;
                        }
                        catch (IOException)
                        {
                            break;
                        }

                        if (!moved)
                        {
                            break;
                        }

                        yield return files.Current;
                    }
                }
            }

            IEnumerator<string>? childDirectories = null;
            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly).GetEnumerator();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            using (childDirectories)
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = childDirectories.MoveNext();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    var childDirectory = childDirectories.Current;
                    if (IsProtectedSystemDirectory(childDirectory))
                    {
                        continue;
                    }

                    directories.Push(childDirectory);
                }
            }
        }
    }

    private static bool IsProtectedSystemDirectory(string fullPath)
    {
        var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> VerifyFileContentAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destinationStream = File.OpenRead(destinationPath);

        using var hasher = SHA256.Create();
        var sourceHash = await hasher.ComputeHashAsync(sourceStream, cancellationToken);
        var destinationHash = await hasher.ComputeHashAsync(destinationStream, cancellationToken);

        return sourceHash.AsSpan().SequenceEqual(destinationHash);
    }
}