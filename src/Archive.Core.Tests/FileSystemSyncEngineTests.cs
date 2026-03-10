using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Infrastructure.Sync;

namespace Archive.Core.Tests;

public sealed class FileSystemSyncEngineTests
{
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
}