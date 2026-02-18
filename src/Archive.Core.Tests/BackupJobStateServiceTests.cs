using Archive.Core.Domain.Entities;
using Archive.Infrastructure.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Archive.Core.Tests;

public class BackupJobStateServiceTests
{
    [Fact]
    public async Task SetEnabledAsync_UpdatesEnabledAndModifiedAt_WhenJobExists()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        var jobId = Guid.NewGuid();

        await using (var seedContext = new ArchiveDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.BackupJobs.Add(new BackupJob
            {
                Id = jobId,
                Name = "Test",
                SourcePath = "C:\\Source",
                DestinationPath = "D:\\Dest",
                Enabled = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ModifiedAt = DateTime.UtcNow.AddMinutes(-5)
            });

            await seedContext.SaveChangesAsync();
        }

        await using (var context = new ArchiveDbContext(options))
        {
            var service = new BackupJobStateService(context);
            var before = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);

            var result = await service.SetEnabledAsync(jobId, enabled: true);

            Assert.True(result);

            var updated = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.True(updated.Enabled);
            Assert.True(updated.ModifiedAt > before.ModifiedAt);
        }
    }

    [Fact]
    public async Task SetEnabledAsync_ReturnsFalse_WhenJobDoesNotExist()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new BackupJobStateService(context);
        var result = await service.SetEnabledAsync(Guid.NewGuid(), enabled: true);

        Assert.False(result);
    }

    [Fact]
    public async Task SoftDeleteAsync_SetsDeletedAtAndModifiedAt_WhenJobExists()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        var jobId = Guid.NewGuid();

        await using (var seedContext = new ArchiveDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.BackupJobs.Add(new BackupJob
            {
                Id = jobId,
                Name = "Delete Me",
                SourcePath = "C:\\Source",
                DestinationPath = "D:\\Dest",
                Enabled = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ModifiedAt = DateTime.UtcNow.AddMinutes(-10),
                DeletedAt = null
            });

            await seedContext.SaveChangesAsync();
        }

        await using (var context = new ArchiveDbContext(options))
        {
            var service = new BackupJobStateService(context);

            var before = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            var result = await service.SoftDeleteAsync(jobId);

            Assert.True(result);

            var updated = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.NotNull(updated.DeletedAt);
            Assert.True(updated.ModifiedAt > before.ModifiedAt);
        }
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsFalse_WhenJobDoesNotExist()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new BackupJobStateService(context);
        var result = await service.SoftDeleteAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateBasicFieldsAsync_UpdatesNameDescriptionEnabledAndModifiedAt_WhenJobExists()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        var jobId = Guid.NewGuid();

        await using (var seedContext = new ArchiveDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.BackupJobs.Add(new BackupJob
            {
                Id = jobId,
                Name = "Original Name",
                Description = "Original Description",
                SourcePath = "C:\\Source",
                DestinationPath = "D:\\Dest",
                Enabled = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ModifiedAt = DateTime.UtcNow.AddMinutes(-10)
            });

            await seedContext.SaveChangesAsync();
        }

        await using (var context = new ArchiveDbContext(options))
        {
            var service = new BackupJobStateService(context);

            var before = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            var result = await service.UpdateBasicFieldsAsync(
                jobId,
                "Updated Name",
                "Updated Description",
                "C:\\UpdatedSource",
                "D:\\UpdatedDest",
                true);

            Assert.True(result);

            var updated = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.Equal("Updated Name", updated.Name);
            Assert.Equal("Updated Description", updated.Description);
            Assert.Equal("C:\\UpdatedSource", updated.SourcePath);
            Assert.Equal("D:\\UpdatedDest", updated.DestinationPath);
            Assert.True(updated.Enabled);
            Assert.True(updated.ModifiedAt > before.ModifiedAt);
        }
    }

    [Fact]
    public async Task UpdateBasicFieldsAsync_ReturnsFalse_WhenJobDoesNotExist()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new BackupJobStateService(context);
        var result = await service.UpdateBasicFieldsAsync(
            Guid.NewGuid(),
            "Any Name",
            "Any Description",
            "C:\\AnySource",
            "D:\\AnyDest",
            true);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateBasicFieldsAsync_ReturnsFalseAndDoesNotPersist_WhenSourceAndDestinationAreEquivalent()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        var jobId = Guid.NewGuid();

        await using (var seedContext = new ArchiveDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.BackupJobs.Add(new BackupJob
            {
                Id = jobId,
                Name = "Original Name",
                Description = "Original Description",
                SourcePath = "C:\\Source",
                DestinationPath = "D:\\Dest",
                Enabled = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ModifiedAt = DateTime.UtcNow.AddMinutes(-10)
            });

            await seedContext.SaveChangesAsync();
        }

        await using (var context = new ArchiveDbContext(options))
        {
            var service = new BackupJobStateService(context);

            var before = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            var result = await service.UpdateBasicFieldsAsync(
                jobId,
                "Updated Name",
                "Updated Description",
                "C:\\Data\\",
                "c:\\data",
                true);

            Assert.False(result);

            var unchanged = await context.BackupJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
            Assert.Equal(before.Name, unchanged.Name);
            Assert.Equal(before.Description, unchanged.Description);
            Assert.Equal(before.SourcePath, unchanged.SourcePath);
            Assert.Equal(before.DestinationPath, unchanged.DestinationPath);
            Assert.Equal(before.Enabled, unchanged.Enabled);
            Assert.Equal(before.ModifiedAt, unchanged.ModifiedAt);
        }
    }
}