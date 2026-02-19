using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;

namespace Archive.Desktop.Tests;

public sealed class JobPreviewServiceTests
{
    [Fact]
    public void BuildPreview_ComputesAddUpdateDeleteAndUnchangedCounts()
    {
        var sourceRoot = CreateTempDirectory();
        var destinationRoot = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "new.txt"), "new-file");
            File.WriteAllText(Path.Combine(sourceRoot, "same.txt"), "same-content");
            File.WriteAllText(Path.Combine(sourceRoot, "changed.txt"), "changed-source");

            File.WriteAllText(Path.Combine(destinationRoot, "same.txt"), "same-content");
            File.WriteAllText(Path.Combine(destinationRoot, "changed.txt"), "changed-destination");
            File.WriteAllText(Path.Combine(destinationRoot, "orphan.txt"), "orphan");

            var alignedUtc = DateTime.UtcNow.AddMinutes(-1);
            File.SetLastWriteTimeUtc(Path.Combine(sourceRoot, "same.txt"), alignedUtc);
            File.SetLastWriteTimeUtc(Path.Combine(destinationRoot, "same.txt"), alignedUtc);

            var now = DateTime.UtcNow;
            var job = new BackupJob
            {
                Id = Guid.NewGuid(),
                Name = "Preview",
                SourcePath = sourceRoot,
                DestinationPath = destinationRoot,
                SyncMode = SyncMode.Mirror,
                ComparisonMethod = ComparisonMethod.Fast,
                OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
                SyncOptions = new SyncOptions
                {
                    Recursive = true,
                    SkipHiddenAndSystem = false
                },
                TriggerType = TriggerType.Manual,
                Enabled = true,
                CreatedAt = now,
                ModifiedAt = now
            };

            var result = JobPreviewService.BuildPreview(job);

            Assert.Equal(1, result.FilesToAdd);
            Assert.Equal(1, result.FilesToUpdate);
            Assert.Equal(1, result.FilesToDelete);
            Assert.Equal(1, result.FilesUnchanged);
            Assert.True(result.TotalBytesToTransfer > 0);
        }
        finally
        {
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(destinationRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildPreview_Throws_WhenSourceIsMissing()
    {
        var destinationRoot = CreateTempDirectory();

        try
        {
            var now = DateTime.UtcNow;
            var job = new BackupJob
            {
                Id = Guid.NewGuid(),
                Name = "Preview",
                SourcePath = Path.Combine(destinationRoot, "missing-source"),
                DestinationPath = destinationRoot,
                SyncMode = SyncMode.Incremental,
                ComparisonMethod = ComparisonMethod.Fast,
                OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
                SyncOptions = new SyncOptions(),
                TriggerType = TriggerType.Manual,
                Enabled = true,
                CreatedAt = now,
                ModifiedAt = now
            };

            Assert.Throws<DirectoryNotFoundException>(() => JobPreviewService.BuildPreview(job));
        }
        finally
        {
            Directory.Delete(destinationRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ArchiveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
