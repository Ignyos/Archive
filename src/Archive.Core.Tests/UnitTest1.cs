using Archive.Core.Configuration;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Core.Sync;
using Archive.Infrastructure.Jobs;
using Archive.Infrastructure.Persistence;
using Archive.Infrastructure.Scheduling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Quartz;

namespace Archive.Core.Tests;

public class AppSettingsBindingTests
{
    [Fact]
    public void AppSettings_Binds_From_Configuration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Archive:MaxConcurrentJobs"] = "3",
                ["Archive:ArchiveScheduleEnabled"] = "false",
                ["Quartz:UseInMemoryStore"] = "true",
                ["Quartz:UseSQLite"] = "false",
                ["Quartz:ConnectionString"] = "Data Source=test.db",
                ["Quartz:MaxConcurrency"] = "7",
                ["Quartz:MisfireThreshold"] = "00:00:30"
            })
            .Build();

        var settings = config.Get<AppSettings>();

        Assert.NotNull(settings);
        Assert.Equal(3, settings!.Archive.MaxConcurrentJobs);
        Assert.False(settings.Archive.ArchiveScheduleEnabled);
        Assert.True(settings.Quartz.UseInMemoryStore);
        Assert.False(settings.Quartz.UseSQLite);
        Assert.Equal("Data Source=test.db", settings.Quartz.ConnectionString);
        Assert.Equal(7, settings.Quartz.MaxConcurrency);
        Assert.Equal("00:00:30", settings.Quartz.MisfireThreshold);
    }
}

public class ArchiveDbContextTests
{
    [Fact]
    public void DbContext_Can_Create_And_Query()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ArchiveDbContext(options))
        {
            context.Database.EnsureCreated();
            context.BackupJobs.Add(new BackupJob
            {
                Id = Guid.NewGuid(),
                SourcePath = "C:\\Source",
                DestinationPath = "D:\\Destination",
                Enabled = true
            });
            context.SaveChanges();
        }

        using (var context = new ArchiveDbContext(options))
        {
            var jobCount = context.BackupJobs.Count();
            Assert.Equal(1, jobCount);
        }
    }
}

public class SyncDecisionServiceTests
{
    [Fact]
    public void Decide_Returns_Copy_When_Destination_Missing()
    {
        var service = new SyncDecisionService();
        var source = new FileSnapshot("C:\\source.txt", 10, new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc));

        var action = service.Decide(source, null, SyncMode.Incremental, ComparisonMethod.Fast, OverwriteBehavior.AlwaysOverwrite);

        Assert.Equal(SyncAction.Copy, action);
    }

    [Fact]
    public void Decide_Returns_Skip_When_Unchanged_In_Fast_Mode()
    {
        var service = new SyncDecisionService();
        var timestamp = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var source = new FileSnapshot("C:\\source.txt", 10, timestamp);
        var destination = new FileSnapshot("D:\\dest.txt", 10, timestamp);

        var action = service.Decide(source, destination, SyncMode.Incremental, ComparisonMethod.Fast, OverwriteBehavior.AlwaysOverwrite);

        Assert.Equal(SyncAction.Skip, action);
    }
}

public class GlobMatcherTests
{
    [Theory]
    [InlineData("*.tmp", "file.tmp", true)]
    [InlineData("*.tmp", "file.txt", false)]
    [InlineData("data-??.log", "data-01.log", true)]
    [InlineData("data-??.log", "data-1.log", false)]
    [InlineData("cache/*", "cache/file.bin", true)]
    [InlineData("cache/*", "other/file.bin", false)]
    public void IsMatch_Evaluates_Basic_Glob_Patterns(string pattern, string input, bool expected)
    {
        var result = GlobMatcher.IsMatch(pattern, input);

        Assert.Equal(expected, result);
    }
}

public class JobExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Creates_Completed_JobExecution_With_Stats()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            SourcePath = "C:\\Source",
            DestinationPath = "D:\\Destination",
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        context.BackupJobs.Add(job);
        await context.SaveChangesAsync();

        var syncEngine = new StubSyncEngine(new SyncResult
        {
            FilesScanned = 5,
            FilesCopied = 2,
            FilesUpdated = 1,
            FilesDeleted = 0,
            FilesSkipped = 2,
            FilesFailed = 0,
            BytesTransferred = 1234,
            ErrorCount = 0,
            WarningCount = 0
        });

        var service = new JobExecutionService(context, syncEngine);
        var execution = await service.ExecuteAsync(job.Id);

        Assert.Equal(JobExecutionStatus.Completed, execution.Status);
        Assert.Equal(5, execution.FilesScanned);
        Assert.Equal(2, execution.FilesCopied);
        Assert.Equal(1, execution.FilesUpdated);
        Assert.Equal(0, execution.FilesDeleted);
        Assert.Equal(2, execution.FilesSkipped);
        Assert.Equal(0, execution.FilesFailed);
        Assert.Equal(1234, execution.BytesTransferred);
        Assert.Equal(0, execution.ErrorCount);
        Assert.Equal(0, execution.WarningCount);
    }

    [Fact]
    public async Task ExecuteAsync_Records_Failure_When_Sync_Throws()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            SourcePath = "C:\\Source",
            DestinationPath = "D:\\Destination",
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        context.BackupJobs.Add(job);
        await context.SaveChangesAsync();

        var syncEngine = new ThrowingSyncEngine();
        var service = new JobExecutionService(context, syncEngine);

        var execution = await service.ExecuteAsync(job.Id);

        Assert.Equal(JobExecutionStatus.Failed, execution.Status);
        Assert.Equal(1, execution.ErrorCount);
        Assert.Single(execution.Logs, log => log.Level == LogLevel.Error);
    }

    private sealed class StubSyncEngine : ISyncEngine
    {
        private readonly SyncResult _result;

        public StubSyncEngine(SyncResult result)
        {
            _result = result;
        }

        public Task<SyncResult> ExecuteAsync(BackupJob job, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingSyncEngine : ISyncEngine
    {
        public Task<SyncResult> ExecuteAsync(BackupJob job, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated failure");
        }
    }
}

public class JobSchedulerServiceTests
{
    [Fact]
    public async Task RunNowAsync_Schedules_And_Triggers_When_Job_Does_Not_Exist()
    {
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new JobSchedulerService(scheduler.Object);
        var jobId = Guid.NewGuid();

        await service.RunNowAsync(jobId);

        scheduler.Verify(x => x.ScheduleJob(
                It.Is<IJobDetail>(job => job.Key.Name.Contains(jobId.ToString())),
                It.IsAny<ITrigger>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        scheduler.Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunNowAsync_Triggers_When_Job_Exists()
    {
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new JobSchedulerService(scheduler.Object);
        var jobId = Guid.NewGuid();

        await service.RunNowAsync(jobId);

        scheduler.Verify(x => x.TriggerJob(
                It.Is<JobKey>(jobKey => jobKey.Name.Contains(jobId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduleJobAsync_Registers_Quartz_Job()
    {
        var scheduler = new Mock<IScheduler>();
        var service = new JobSchedulerService(scheduler.Object);
        var jobId = Guid.NewGuid();

        await service.ScheduleJobAsync(jobId);

        scheduler.Verify(x => x.ScheduleJob(
                It.Is<IJobDetail>(job => job.Key.Name.Contains(jobId.ToString())),
                It.IsAny<ITrigger>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_Interrupts_Quartz_Job()
    {
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new JobSchedulerService(scheduler.Object);
        var jobId = Guid.NewGuid();

        var stopped = await service.StopAsync(jobId);

        Assert.True(stopped);
        scheduler.Verify(x => x.Interrupt(
                It.Is<JobKey>(jobKey => jobKey.Name.Contains(jobId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Quartz_Job()
    {
        var scheduler = new Mock<IScheduler>();
        scheduler
            .Setup(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new JobSchedulerService(scheduler.Object);
        var jobId = Guid.NewGuid();

        var deleted = await service.DeleteAsync(jobId);

        Assert.True(deleted);
        scheduler.Verify(x => x.DeleteJob(
                It.Is<JobKey>(jobKey => jobKey.Name.Contains(jobId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
