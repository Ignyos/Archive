using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Sync;
using System.IO;

namespace Archive.Desktop;

public static class JobPreviewService
{
    public static JobPreviewResult BuildPreview(BackupJob job)
    {
        var sourceFiles = EnumerateSourceFiles(job).ToList();
        var destinationLookup = BuildDestinationLookup(job);
        var decisionService = new SyncDecisionService();

        var filesToAdd = 0;
        var filesToUpdate = 0;
        var filesUnchanged = 0;
        var filesSkipped = 0;
        long bytesToTransfer = 0;

        var sourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFile in sourceFiles)
        {
            sourceKeys.Add(sourceFile.Key);

            if (job.SyncOptions?.SkipHiddenAndSystem == true && IsHiddenOrSystem(sourceFile.Path))
            {
                filesSkipped++;
                continue;
            }

            destinationLookup.TryGetValue(sourceFile.Key, out var destinationPath);
            var destinationSnapshot = destinationPath is null
                ? null
                : BuildSnapshot(destinationPath);

            var action = decisionService.Decide(
                sourceFile.Snapshot,
                destinationSnapshot,
                job.SyncMode,
                job.ComparisonMethod,
                job.OverwriteBehavior);

            switch (action)
            {
                case SyncAction.Copy:
                    filesToAdd++;
                    bytesToTransfer += sourceFile.Snapshot.Size;
                    break;
                case SyncAction.Update:
                    filesToUpdate++;
                    bytesToTransfer += sourceFile.Snapshot.Size;
                    break;
                default:
                    filesUnchanged++;
                    break;
            }
        }

        var filesToDelete = 0;
        if (job.SyncMode == SyncMode.Mirror)
        {
            filesToDelete = destinationLookup.Keys.Count(key => !sourceKeys.Contains(key));
        }

        return new JobPreviewResult
        {
            FilesToAdd = filesToAdd,
            FilesToUpdate = filesToUpdate,
            FilesToDelete = filesToDelete,
            FilesUnchanged = filesUnchanged,
            FilesSkipped = filesSkipped,
            TotalBytesToTransfer = bytesToTransfer
        };
    }

    private static IEnumerable<(string Key, string Path, FileSnapshot Snapshot)> EnumerateSourceFiles(BackupJob job)
    {
        if (File.Exists(job.SourcePath))
        {
            var fileInfo = new FileInfo(job.SourcePath);
            yield return (
                fileInfo.Name,
                fileInfo.FullName,
                new FileSnapshot(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc));
            yield break;
        }

        if (!Directory.Exists(job.SourcePath))
        {
            throw new DirectoryNotFoundException($"Source path not found: {job.SourcePath}");
        }

        var recursive = job.SyncOptions?.Recursive ?? true;
        foreach (var file in EnumerateFilesSafe(job.SourcePath, recursive))
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(job.SourcePath, fileInfo.FullName);
            yield return (
                relativePath,
                fileInfo.FullName,
                new FileSnapshot(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc));
        }
    }

    private static Dictionary<string, string> BuildDestinationLookup(BackupJob job)
    {
        if (!Directory.Exists(job.DestinationPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(job.SourcePath))
        {
            var sourceName = Path.GetFileName(job.SourcePath);
            var destinationFilePath = Path.Combine(job.DestinationPath, sourceName);
            if (File.Exists(destinationFilePath))
            {
                lookup[sourceName] = destinationFilePath;
            }

            return lookup;
        }

        var recursive = job.SyncOptions?.Recursive ?? true;

        foreach (var file in EnumerateFilesSafe(job.DestinationPath, recursive))
        {
            var relativePath = Path.GetRelativePath(job.DestinationPath, file);
            lookup[relativePath] = file;
        }

        return lookup;
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
                while (topLevelFiles.MoveNext())
                {
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
                    while (files.MoveNext())
                    {
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
                while (childDirectories.MoveNext())
                {
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

    private static FileSnapshot BuildSnapshot(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        return new FileSnapshot(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc);
    }

    private static bool IsHiddenOrSystem(string fullPath)
    {
        var attributes = File.GetAttributes(fullPath);
        return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
    }
}
