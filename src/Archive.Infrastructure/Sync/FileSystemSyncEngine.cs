using System.Security.Cryptography;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Sync;

namespace Archive.Infrastructure.Sync;

public sealed class FileSystemSyncEngine : ISyncEngine
{
    private readonly SyncDecisionService _decisionService = new();

    public async Task<SyncResult> ExecuteAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        var filesScanned = 0;
        var filesCopied = 0;
        var filesUpdated = 0;
        var filesDeleted = 0;
        var filesSkipped = 0;
        var filesFailed = 0;
        var bytesTransferred = 0L;
        var errorCount = 0;
        var warningCount = 0;

        if (File.Exists(job.SourcePath))
        {
            return await ExecuteSingleFileAsync(job, cancellationToken);
        }

        if (!Directory.Exists(job.SourcePath))
        {
            throw new DirectoryNotFoundException($"Source path not found: {job.SourcePath}");
        }

        Directory.CreateDirectory(job.DestinationPath);

        var recursive = job.SyncOptions?.Recursive ?? true;
        var skipHiddenAndSystem = job.SyncOptions?.SkipHiddenAndSystem ?? true;
        var shouldDeleteOrphans = job.SyncMode == SyncMode.Mirror || (job.SyncOptions?.DeleteOrphaned ?? false);
        var exclusionPatterns = (job.BackupJobExclusionPatterns ?? Array.Empty<BackupJobExclusionPattern>())
            .Select(x => x.ExclusionPattern?.Pattern)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();

        var sourceFiles = Directory.EnumerateFiles(
            job.SourcePath,
            "*",
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        var sourceRelativeToFullPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFullPath in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceRelativePath = Path.GetRelativePath(job.SourcePath, sourceFullPath);

            if (IsExcluded(sourceRelativePath, exclusionPatterns))
            {
                filesSkipped++;
                continue;
            }

            if (skipHiddenAndSystem && IsHiddenOrSystem(sourceFullPath))
            {
                filesSkipped++;
                continue;
            }

            sourceRelativeToFullPath[sourceRelativePath] = sourceFullPath;
            filesScanned++;

            var destinationFullPath = Path.Combine(job.DestinationPath, sourceRelativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFullPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var sourceInfo = new FileInfo(sourceFullPath);
            var sourceSnapshot = new FileSnapshot(sourceFullPath, sourceInfo.Length, sourceInfo.LastWriteTimeUtc);

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

            try
            {
                switch (action)
                {
                    case SyncAction.Copy:
                        await CopyFileAsync(sourceFullPath, destinationFullPath, overwrite: true, cancellationToken);
                        filesCopied++;
                        bytesTransferred += sourceInfo.Length;
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
                        break;

                    case SyncAction.Skip:
                        filesSkipped++;
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
                    }
                }
            }
            catch
            {
                filesFailed++;
                errorCount++;
            }
        }

        if (shouldDeleteOrphans)
        {
            var destinationFiles = Directory.EnumerateFiles(
                job.DestinationPath,
                "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            foreach (var destinationFullPath in destinationFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destinationRelativePath = Path.GetRelativePath(job.DestinationPath, destinationFullPath);

                if (IsExcluded(destinationRelativePath, exclusionPatterns))
                {
                    continue;
                }

                if (sourceRelativeToFullPath.ContainsKey(destinationRelativePath))
                {
                    continue;
                }

                try
                {
                    File.Delete(destinationFullPath);
                    filesDeleted++;
                }
                catch
                {
                    filesFailed++;
                    errorCount++;
                }
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
            WarningCount = warningCount
        };
    }

    private static async Task<SyncResult> ExecuteSingleFileAsync(BackupJob job, CancellationToken cancellationToken)
    {
        var sourceInfo = new FileInfo(job.SourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("Source file not found.", job.SourcePath);
        }

        Directory.CreateDirectory(job.DestinationPath);
        var destinationFullPath = Path.Combine(job.DestinationPath, sourceInfo.Name);

        try
        {
            await CopyFileAsync(job.SourcePath, destinationFullPath, overwrite: true, cancellationToken);
            return new SyncResult
            {
                FilesScanned = 1,
                FilesCopied = 1,
                BytesTransferred = sourceInfo.Length
            };
        }
        catch
        {
            return new SyncResult
            {
                FilesScanned = 1,
                FilesFailed = 1,
                ErrorCount = 1
            };
        }
    }

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

    private static bool IsExcluded(string relativePath, IReadOnlyCollection<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (GlobMatcher.IsMatch(pattern, relativePath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHiddenOrSystem(string path)
    {
        var attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
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