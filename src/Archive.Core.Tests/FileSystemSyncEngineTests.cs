using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Core.Sync;
using Archive.Infrastructure.Sync;

namespace Archive.Core.Tests;

public sealed class FileSystemSyncEngineTests
{
    [Fact]
    public async Task ExecuteAsync_Skips_Full_Comparison_When_SourceFingerprint_Is_Unchanged()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-src-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-dst-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(destinationRoot);

        var sourceFile = Path.Combine(sourceRoot, "a.txt");
        await File.WriteAllTextAsync(sourceFile, "hello");

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Name = "Fingerprint No-Change",
            JobType = JobType.DirectorySync,
            SourcePath = sourceRoot,
            DestinationPath = destinationRoot,
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            SyncOptions = new SyncOptions
            {
                Recursive = true,
                SkipHiddenAndSystem = true,
                DeleteOrphaned = false,
                VerifyAfterCopy = false
            }
        };

        var scanStateStore = new InMemorySourceScanStateStore();
        var engine = new FileSystemSyncEngine(scanStateStore);

        try
        {
            var first = await engine.ExecuteAsync(job);
            var second = await engine.ExecuteAsync(job);

            Assert.Equal(1, first.FilesCopied);
            Assert.Equal(0, second.FilesCopied);
            Assert.Equal(0, second.FilesUpdated);
            Assert.Contains(second.OperationLogs, x => x.Message.Contains("No source changes detected", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_Does_Not_Skip_Mirror_Orphan_Delete_When_Source_Unchanged()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-src-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-dst-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(destinationRoot);

        var sourceFile = Path.Combine(sourceRoot, "a.txt");
        await File.WriteAllTextAsync(sourceFile, "hello");

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Name = "Mirror Safety",
            JobType = JobType.DirectorySync,
            SourcePath = sourceRoot,
            DestinationPath = destinationRoot,
            Enabled = true,
            SyncMode = SyncMode.Mirror,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            SyncOptions = new SyncOptions
            {
                Recursive = true,
                SkipHiddenAndSystem = true,
                DeleteOrphaned = false,
                VerifyAfterCopy = false
            }
        };

        var scanStateStore = new InMemorySourceScanStateStore();
        var engine = new FileSystemSyncEngine(scanStateStore);

        try
        {
            await engine.ExecuteAsync(job);

            var orphan = Path.Combine(destinationRoot, "orphan.txt");
            await File.WriteAllTextAsync(orphan, "orphan");

            var second = await engine.ExecuteAsync(job);

            Assert.Equal(1, second.FilesDeleted);
            Assert.False(File.Exists(orphan));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ManualJob_Publishes_Progress_Notifications()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-src-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-dst-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(destinationRoot);

        var sourceFile = Path.Combine(sourceRoot, "data.bin");
        var fileBytes = new byte[2048];
        new Random(42).NextBytes(fileBytes);
        await File.WriteAllBytesAsync(sourceFile, fileBytes);

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Name = "Manual Progress Test",
            JobType = JobType.DirectorySync,
            SourcePath = sourceRoot,
            DestinationPath = destinationRoot,
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            SyncOptions = new SyncOptions
            {
                Recursive = true,
                SkipHiddenAndSystem = true,
                DeleteOrphaned = false,
                VerifyAfterCopy = false
            }
        };

        var notifications = new List<JobExecutionNotificationEvent>();
        void Handler(JobExecutionNotificationEvent evt)
        {
            if (evt.JobId == job.Id)
            {
                notifications.Add(evt);
            }
        }

        JobExecutionNotificationHub.Published += Handler;
        try
        {
            var engine = new FileSystemSyncEngine();
            var result = await engine.ExecuteAsync(job);

            Assert.Equal(1, result.FilesCopied);
            Assert.Contains(notifications, x =>
                x.Kind == JobExecutionNotificationKind.Progress &&
                x.IsManualRun &&
                x.ProgressTotalBytes == fileBytes.Length &&
                x.ProgressCompletedBytes == 0);
            Assert.Contains(notifications, x =>
                x.Kind == JobExecutionNotificationKind.Progress &&
                x.IsManualRun &&
                x.ProgressTotalBytes == fileBytes.Length &&
                x.ProgressCompletedBytes == fileBytes.Length);
        }
        finally
        {
            JobExecutionNotificationHub.Published -= Handler;

            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ExecuteAsync_Skips_Inaccessible_Subdirectories()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sourceRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-src-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"archive-sync-dst-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(destinationRoot);

        var deniedDirectory = Path.Combine(sourceRoot, "denied");
        var allowedDirectory = Path.Combine(sourceRoot, "allowed");
        Directory.CreateDirectory(deniedDirectory);
        Directory.CreateDirectory(allowedDirectory);

        var allowedFile = Path.Combine(allowedDirectory, "ok.txt");
        var deniedFile = Path.Combine(deniedDirectory, "blocked.txt");
        await File.WriteAllTextAsync(allowedFile, "ok");
        await File.WriteAllTextAsync(deniedFile, "blocked");

        var accessDeniedApplied = TryApplyAccessDeniedRule(deniedDirectory);
        if (!accessDeniedApplied)
        {
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(destinationRoot, recursive: true);
            return;
        }

        var engine = new FileSystemSyncEngine();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            JobType = JobType.DirectorySync,
            SourcePath = sourceRoot,
            DestinationPath = destinationRoot,
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            SyncOptions = new SyncOptions
            {
                Recursive = true,
                SkipHiddenAndSystem = true,
                DeleteOrphaned = false,
                VerifyAfterCopy = false
            }
        };

        try
        {
            var result = await engine.ExecuteAsync(job);

            Assert.Equal(1, result.FilesCopied);
            Assert.Equal(1, result.FilesScanned);
            Assert.True(File.Exists(Path.Combine(destinationRoot, "allowed", "ok.txt")));
            Assert.False(File.Exists(Path.Combine(destinationRoot, "denied", "blocked.txt")));
        }
        finally
        {
            RemoveAccessDeniedRule(deniedDirectory);

            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryApplyAccessDeniedRule(string deniedDirectory)
    {
        try
        {
            var currentIdentity = WindowsIdentity.GetCurrent();
            if (currentIdentity.User is null)
            {
                return false;
            }

            var directoryInfo = new DirectoryInfo(deniedDirectory);
            var accessControl = directoryInfo.GetAccessControl();
            accessControl.AddAccessRule(new FileSystemAccessRule(
                currentIdentity.User,
                FileSystemRights.ReadData | FileSystemRights.ListDirectory,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny));

            directoryInfo.SetAccessControl(accessControl);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveAccessDeniedRule(string deniedDirectory)
    {
        try
        {
            var currentIdentity = WindowsIdentity.GetCurrent();
            if (currentIdentity.User is null)
            {
                return;
            }

            var directoryInfo = new DirectoryInfo(deniedDirectory);
            var accessControl = directoryInfo.GetAccessControl();
            accessControl.RemoveAccessRuleAll(new FileSystemAccessRule(
                currentIdentity.User,
                FileSystemRights.ReadData | FileSystemRights.ListDirectory,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny));

            directoryInfo.SetAccessControl(accessControl);
        }
        catch
        {
        }
    }

    private sealed class InMemorySourceScanStateStore : ISourceScanStateStore
    {
        private readonly Dictionary<Guid, SourceScanState> _states = new();

        public Task<SourceScanState?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_states.TryGetValue(jobId, out var state) ? state : null);
        }

        public Task SaveAsync(SourceScanState state, CancellationToken cancellationToken = default)
        {
            _states[state.JobId] = state;
            return Task.CompletedTask;
        }
    }
}