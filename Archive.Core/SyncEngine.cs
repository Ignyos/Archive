using System.Diagnostics;
using System.Security.Cryptography;

namespace Archive.Core;

/// <summary>
/// Default implementation of directory synchronization engine.
/// </summary>
public class SyncEngine : ISyncEngine
{
    /// <inheritdoc/>
    public async Task<SyncResult> SynchronizeAsync(
        string sourcePath,
        string destinationPath,
        SyncOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate paths
            if (!Directory.Exists(sourcePath))
            {
                result.Errors.Add($"Source directory does not exist: {sourcePath}");
                result.Success = false;
                result.StoppedReason = SyncStoppedReason.Error;
                return result;
            }

            // Create destination directory if it doesn't exist
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            // Perform synchronization
            await SyncDirectoryAsync(
                sourcePath,
                destinationPath,
                options,
                result,
                cancellationToken);

            result.Success = result.Errors.Count == 0;
            result.StoppedReason = SyncStoppedReason.Completed;
        }
        catch (OperationCanceledException)
        {
            result.StoppedReason = SyncStoppedReason.Cancelled;
            result.Success = false;
            result.Warnings.Add("Synchronization was cancelled.");
        }
        catch (ScheduleBoundaryException)
        {
            result.StoppedReason = SyncStoppedReason.ScheduleBoundary;
            result.Success = false;
            result.Warnings.Add("Synchronization stopped due to schedule boundary.");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Synchronization failed: {ex.Message}");
            result.Success = false;
            result.StoppedReason = SyncStoppedReason.Error;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task SyncDirectoryAsync(
        string sourcePath,
        string destinationPath,
        SyncOptions options,
        SyncResult result,
        CancellationToken cancellationToken)
    {
        var sourceDir = new DirectoryInfo(sourcePath);
        var destDir = new DirectoryInfo(destinationPath);

        // Get all files in source directory
        var sourceFiles = sourceDir.GetFiles();

        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if operation should continue (for schedule boundaries)
            if (options.ShouldContinue != null && !options.ShouldContinue())
            {
                throw new ScheduleBoundaryException("Operation stopped due to schedule constraint.");
            }

            // Check if file matches exclude patterns
            if (IsExcluded(sourceFile.Name, options.ExcludePatterns))
            {
                result.FilesSkipped++;
                continue;
            }

            var destFilePath = Path.Combine(destinationPath, sourceFile.Name);
            var destFile = new FileInfo(destFilePath);

            try
            {
                // Determine if file needs to be copied
                if (!destFile.Exists)
                {
                    var operation = new SyncOperation
                    {
                        Type = SyncOperationType.Copy,
                        SourcePath = sourceFile.FullName,
                        DestinationPath = destFilePath,
                        FileSize = sourceFile.Length,
                        Description = $"Copy new file: {sourceFile.Name}"
                    };

                    if (options.DryRun)
                    {
                        result.PlannedOperations.Add(operation);
                    }
                    else
                    {
                        await CopyFileAsync(sourceFile, destFile, options, cancellationToken);
                        options.OperationLogger?.Invoke(operation);
                    }
                    result.FilesCopied++;
                    result.BytesTransferred += sourceFile.Length;
                }
                else if (NeedsUpdate(sourceFile, destFile))
                {
                    if (options.Overwrite)
                    {
                        var operation = new SyncOperation
                        {
                            Type = SyncOperationType.Update,
                            SourcePath = sourceFile.FullName,
                            DestinationPath = destFilePath,
                            FileSize = sourceFile.Length,
                            Description = $"Update existing file: {sourceFile.Name}"
                        };

                        if (options.DryRun)
                        {
                            result.PlannedOperations.Add(operation);
                        }
                        else
                        {
                            await CopyFileAsync(sourceFile, destFile, options, cancellationToken);
                            options.OperationLogger?.Invoke(operation);
                        }
                        result.FilesUpdated++;
                        result.BytesTransferred += sourceFile.Length;
                    }
                    else
                    {
                        result.FilesSkipped++;
                    }
                }
                else
                {
                    var operation = new SyncOperation
                    {
                        Type = SyncOperationType.Skip,
                        SourcePath = sourceFile.FullName,
                        DestinationPath = destFilePath,
                        FileSize = sourceFile.Length,
                        Description = $"Skip (already up-to-date): {sourceFile.Name}"
                    };

                    if (options.DryRun)
                    {
                        result.PlannedOperations.Add(operation);
                    }
                    else
                    {
                        options.OperationLogger?.Invoke(operation);
                    }
                    result.FilesSkipped++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to copy {sourceFile.FullName}: {ex.Message}");
            }
        }

        // Handle orphaned files in destination
        if (options.DeleteOrphaned)
        {
            var destFiles = destDir.GetFiles();
            foreach (var destFile in destFiles)
            {
                var sourceFilePath = Path.Combine(sourcePath, destFile.Name);
                if (!File.Exists(sourceFilePath))
                {
                    try
                    {
                        var operation = new SyncOperation
                        {
                            Type = SyncOperationType.Delete,
                            DestinationPath = destFile.FullName,
                            FileSize = destFile.Length,
                            Description = $"Delete orphaned file: {destFile.Name}"
                        };

                        if (options.DryRun)
                        {
                            result.PlannedOperations.Add(operation);
                        }
                        else
                        {
                            destFile.Delete();
                            options.OperationLogger?.Invoke(operation);
                        }
                        result.FilesDeleted++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to delete {destFile.FullName}: {ex.Message}");
                    }
                }
            }
        }

        // Recursively sync subdirectories
        if (options.Recursive)
        {
            DirectoryInfo[] sourceSubDirs;
            try
            {
                sourceSubDirs = sourceDir.GetDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                result.Warnings.Add($"Access denied to directory: {sourceDir.FullName}");
                return;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to list subdirectories in {sourceDir.FullName}: {ex.Message}");
                return;
            }

            foreach (var sourceSubDir in sourceSubDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if operation should continue
                if (options.ShouldContinue != null && !options.ShouldContinue())
                {
                    throw new ScheduleBoundaryException("Operation stopped due to schedule constraint.");
                }

                // Check if directory matches exclude patterns
                if (IsExcluded(sourceSubDir.Name, options.ExcludePatterns))
                {
                    result.Warnings.Add($"Excluded directory: {sourceSubDir.Name}");
                    continue;
                }

                var destSubDirPath = Path.Combine(destinationPath, sourceSubDir.Name);
                
                try
                {
                    if (!Directory.Exists(destSubDirPath))
                    {
                        Directory.CreateDirectory(destSubDirPath);
                    }

                    await SyncDirectoryAsync(
                        sourceSubDir.FullName,
                        destSubDirPath,
                        options,
                        result,
                        cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    result.Warnings.Add($"Access denied to directory: {sourceSubDir.FullName}");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to sync directory {sourceSubDir.FullName}: {ex.Message}");
                }
            }

            // Handle orphaned directories
            if (options.DeleteOrphaned)
            {
                var destSubDirs = destDir.GetDirectories();
                foreach (var destSubDir in destSubDirs)
                {
                    var sourceSubDirPath = Path.Combine(sourcePath, destSubDir.Name);
                    if (!Directory.Exists(sourceSubDirPath))
                    {
                        try
                        {
                            // Recursively log all files and subdirectories in the orphaned directory
                            var operations = new List<SyncOperation>();
                            CollectDirectoryDeletions(destSubDir, operations);

                            if (options.DryRun)
                            {
                                result.PlannedOperations.AddRange(operations);
                            }
                            else
                            {
                                destSubDir.Delete(true);
                                // Log all the deletion operations
                                foreach (var operation in operations)
                                {
                                    options.OperationLogger?.Invoke(operation);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Failed to delete directory {destSubDir.FullName}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    private async Task CopyFileAsync(
        FileInfo source,
        FileInfo destination,
        SyncOptions options,
        CancellationToken cancellationToken)
    {
        // Copy file
        using (var sourceStream = source.OpenRead())
        using (var destStream = destination.Create())
        {
            await sourceStream.CopyToAsync(destStream, cancellationToken);
        }

        // Preserve timestamps if requested
        if (options.PreserveTimestamps)
        {
            destination.LastWriteTimeUtc = source.LastWriteTimeUtc;
            destination.CreationTimeUtc = source.CreationTimeUtc;
        }

        // Verify file after copy if requested
        if (options.VerifyAfterCopy)
        {
            if (!await VerifyFilesMatchAsync(source, destination, cancellationToken))
            {
                throw new IOException($"File verification failed for {destination.FullName}");
            }
        }
    }

    private bool NeedsUpdate(FileInfo source, FileInfo destination)
    {
        // Compare file size and last write time
        return source.Length != destination.Length ||
               source.LastWriteTimeUtc > destination.LastWriteTimeUtc;
    }

    private bool IsExcluded(string fileName, List<string> excludePatterns)
    {
        if (excludePatterns == null || excludePatterns.Count == 0)
            return false;

        foreach (var pattern in excludePatterns)
        {
            if (MatchesPattern(fileName, pattern))
                return true;
        }

        return false;
    }

    private bool MatchesPattern(string fileName, string pattern)
    {
        // Simple wildcard pattern matching
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            fileName,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private void CollectDirectoryDeletions(DirectoryInfo directory, List<SyncOperation> operations)
    {
        // First, recursively collect all subdirectories
        foreach (var subDir in directory.GetDirectories())
        {
            CollectDirectoryDeletions(subDir, operations);
        }

        // Then collect all files in this directory
        foreach (var file in directory.GetFiles())
        {
            operations.Add(new SyncOperation
            {
                Type = SyncOperationType.Delete,
                DestinationPath = file.FullName,
                Description = $"Delete orphaned file: {file.Name}"
            });
        }

        // Finally, add the directory itself
        operations.Add(new SyncOperation
        {
            Type = SyncOperationType.Delete,
            DestinationPath = directory.FullName,
            Description = $"Delete orphaned directory: {directory.Name}"
        });
    }

    private async Task<bool> VerifyFilesMatchAsync(
        FileInfo source,
        FileInfo destination,
        CancellationToken cancellationToken)
    {
        if (source.Length != destination.Length)
            return false;

        using var sourceHash = SHA256.Create();
        using var destHash = SHA256.Create();

        using (var sourceStream = source.OpenRead())
        {
            var sourceHashBytes = await sourceHash.ComputeHashAsync(sourceStream, cancellationToken);
            
            using var destStream = destination.OpenRead();
            var destHashBytes = await destHash.ComputeHashAsync(destStream, cancellationToken);

            return sourceHashBytes.SequenceEqual(destHashBytes);
        }
    }
}
