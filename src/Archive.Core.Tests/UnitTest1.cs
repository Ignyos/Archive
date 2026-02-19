using Archive.Core.Configuration;
using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Core.Sync;
using Archive.Infrastructure.Configuration;
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

    [Fact]
    public async Task ExecuteAsync_Sets_CompletedWithWarnings_When_ResultContainsWarnings()
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
            FilesScanned = 3,
            FilesCopied = 1,
            FilesUpdated = 1,
            FilesDeleted = 0,
            FilesSkipped = 1,
            FilesFailed = 0,
            BytesTransferred = 100,
            ErrorCount = 0,
            WarningCount = 1
        });

        var service = new JobExecutionService(context, syncEngine);
        var execution = await service.ExecuteAsync(job.Id);

        Assert.Equal(JobExecutionStatus.CompletedWithWarnings, execution.Status);
        Assert.Equal(1, execution.WarningCount);
    }

    [Fact]
    public async Task ExecuteAsync_Invokes_RetentionPrune_After_Execution()
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
            FilesScanned = 1,
            FilesCopied = 1,
            BytesTransferred = 1
        });

        var retentionService = new Mock<IExecutionLogRetentionService>();
        retentionService
            .Setup(x => x.PruneAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new JobExecutionService(context, syncEngine, retentionService.Object);
        await service.ExecuteAsync(job.Id);

        retentionService.Verify(x => x.PruneAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Publishes_Start_And_Completed_Notifications()
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
            Name = "Notify Success",
            SourcePath = "C:\\Source",
            DestinationPath = "D:\\Destination",
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            NotifyOnStart = true,
            NotifyOnComplete = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        context.BackupJobs.Add(job);
        await context.SaveChangesAsync();

        var syncEngine = new StubSyncEngine(new SyncResult
        {
            FilesScanned = 1,
            FilesCopied = 1,
            FilesUpdated = 0,
            FilesDeleted = 0,
            FilesSkipped = 0,
            FilesFailed = 0,
            BytesTransferred = 10,
            ErrorCount = 0,
            WarningCount = 0
        });

        var published = new List<JobExecutionNotificationEvent>();
        void Handler(JobExecutionNotificationEvent evt)
        {
            if (evt.JobId == job.Id)
            {
                published.Add(evt);
            }
        }
        JobExecutionNotificationHub.Published += Handler;

        try
        {
            var service = new JobExecutionService(context, syncEngine);
            await service.ExecuteAsync(job.Id);
        }
        finally
        {
            JobExecutionNotificationHub.Published -= Handler;
        }

        Assert.Equal(2, published.Count);
        Assert.Equal(JobExecutionNotificationKind.Started, published[0].Kind);
        Assert.Equal(JobExecutionNotificationKind.Completed, published[1].Kind);
        Assert.Equal("Notify Success", published[0].JobName);
        Assert.True(published[0].NotifyOnStartOverride);
        Assert.True(published[1].NotifyOnCompleteOverride);
    }

    [Fact]
    public async Task ExecuteAsync_Publishes_Failed_Notification_When_Sync_Throws()
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
            Name = "Notify Failure",
            SourcePath = "C:\\Source",
            DestinationPath = "D:\\Destination",
            Enabled = true,
            SyncMode = SyncMode.Incremental,
            ComparisonMethod = ComparisonMethod.Fast,
            OverwriteBehavior = OverwriteBehavior.AlwaysOverwrite,
            TriggerType = TriggerType.Manual,
            NotifyOnFail = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        context.BackupJobs.Add(job);
        await context.SaveChangesAsync();

        var syncEngine = new ThrowingSyncEngine();

        var published = new List<JobExecutionNotificationEvent>();
        void Handler(JobExecutionNotificationEvent evt)
        {
            if (evt.JobId == job.Id)
            {
                published.Add(evt);
            }
        }
        JobExecutionNotificationHub.Published += Handler;

        try
        {
            var service = new JobExecutionService(context, syncEngine);
            await service.ExecuteAsync(job.Id);
        }
        finally
        {
            JobExecutionNotificationHub.Published -= Handler;
        }

        Assert.Equal(2, published.Count);
        Assert.Equal(JobExecutionNotificationKind.Started, published[0].Kind);
        Assert.Equal(JobExecutionNotificationKind.Failed, published[1].Kind);
        Assert.Equal(JobExecutionStatus.Failed, published[1].Status);
        Assert.True(published[1].NotifyOnFailOverride);
        Assert.Contains("Simulated failure", published[1].DetailSummary);
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

public class ArchiveScheduleControlServiceTests
{
    [Fact]
    public async Task InitializeAsync_PersistsDefaultAndPausesScheduler_WhenDefaultDisabled()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var scheduler = new Mock<IScheduler>();
        scheduler.SetupGet(x => x.IsStarted).Returns(false);

        var settings = new AppSettings
        {
            Archive = new ArchiveSettings
            {
                ArchiveScheduleEnabled = false
            }
        };

        var service = new ArchiveScheduleControlService(context, scheduler.Object, settings);

        await service.InitializeAsync();

        scheduler.Verify(x => x.Start(It.IsAny<CancellationToken>()), Times.Once);
        scheduler.Verify(x => x.PauseAll(It.IsAny<CancellationToken>()), Times.Once);

        var persisted = await context.AppSettings.AsNoTracking().SingleAsync(x => x.Key == "ArchiveScheduleEnabled");
        Assert.Equal("False", persisted.Value);
    }

    [Fact]
    public async Task SetScheduleEnabledAsync_UpdatesPersistenceAndResumesScheduler_WhenEnabled()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.AppSettings.Add(new AppSetting
        {
            Key = "ArchiveScheduleEnabled",
            Value = "False"
        });
        await context.SaveChangesAsync();

        var scheduler = new Mock<IScheduler>();
        scheduler.SetupGet(x => x.IsStarted).Returns(true);

        var settings = new AppSettings();
        var service = new ArchiveScheduleControlService(context, scheduler.Object, settings);

        await service.SetScheduleEnabledAsync(true);

        scheduler.Verify(x => x.ResumeAll(It.IsAny<CancellationToken>()), Times.Once);
        scheduler.Verify(x => x.PauseAll(It.IsAny<CancellationToken>()), Times.Never);

        var persisted = await context.AppSettings.AsNoTracking().SingleAsync(x => x.Key == "ArchiveScheduleEnabled");
        Assert.Equal("True", persisted.Value);
    }
}

public class ArchiveApplicationSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_Returns_Defaults_When_NoRowsExist()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new ArchiveApplicationSettingsService(context);
        var settings = await service.GetAsync();

        Assert.False(settings.RunOnWindowsStartup);
        Assert.True(settings.EnableNotifications);
        Assert.False(settings.NotifyOnStart);
        Assert.True(settings.NotifyOnComplete);
        Assert.True(settings.NotifyOnFail);
        Assert.True(settings.PlayNotificationSound);
        Assert.Equal(14, settings.LogRetentionValue);
        Assert.Equal(LogRetentionUnit.Days, settings.LogRetentionUnit);
        Assert.False(settings.EnableVerboseLogging);
    }

    [Fact]
    public async Task SetAsync_Persists_And_GetAsync_Reads_Back_AllValues()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ArchiveDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new ArchiveApplicationSettingsService(context);

        var expected = new ArchiveApplicationSettings
        {
            RunOnWindowsStartup = true,
            EnableNotifications = true,
            NotifyOnStart = true,
            NotifyOnComplete = false,
            NotifyOnFail = true,
            PlayNotificationSound = false,
            LogRetentionValue = 2,
            LogRetentionUnit = LogRetentionUnit.Months,
            EnableVerboseLogging = true
        };

        await service.SetAsync(expected);

        var actual = await service.GetAsync();

        Assert.Equal(expected.RunOnWindowsStartup, actual.RunOnWindowsStartup);
        Assert.Equal(expected.EnableNotifications, actual.EnableNotifications);
        Assert.Equal(expected.NotifyOnStart, actual.NotifyOnStart);
        Assert.Equal(expected.NotifyOnComplete, actual.NotifyOnComplete);
        Assert.Equal(expected.NotifyOnFail, actual.NotifyOnFail);
        Assert.Equal(expected.PlayNotificationSound, actual.PlayNotificationSound);
        Assert.Equal(expected.LogRetentionValue, actual.LogRetentionValue);
        Assert.Equal(expected.LogRetentionUnit, actual.LogRetentionUnit);
        Assert.Equal(expected.EnableVerboseLogging, actual.EnableVerboseLogging);
    }
}

public class ExecutionLogRetentionServiceTests
{
    [Fact]
    public async Task PruneAsync_Removes_Logs_Older_Than_Days_Cutoff()
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
            Name = "Retention Job",
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

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Status = JobExecutionStatus.Completed,
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            EndTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(1)
        };

        context.JobExecutions.Add(execution);

        context.ExecutionLogs.AddRange(
            new ExecutionLog
            {
                Id = Guid.NewGuid(),
                JobExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow.AddDays(-10),
                Level = LogLevel.Warning,
                Message = "Old warning"
            },
            new ExecutionLog
            {
                Id = Guid.NewGuid(),
                JobExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Level = LogLevel.Info,
                Message = "Recent info"
            });

        await context.SaveChangesAsync();

        var settingsService = new Mock<IArchiveApplicationSettingsService>();
        settingsService
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArchiveApplicationSettings
            {
                LogRetentionValue = 7,
                LogRetentionUnit = LogRetentionUnit.Days
            });

        var retentionService = new ExecutionLogRetentionService(context, settingsService.Object);
        await retentionService.PruneAsync();

        var logs = await context.ExecutionLogs.AsNoTracking().ToListAsync();
        Assert.Single(logs);
        Assert.Equal("Recent info", logs[0].Message);
    }

    [Fact]
    public async Task PruneAsync_Does_Not_Remove_Logs_When_Retention_Is_Zero()
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
            Name = "Retention Job",
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

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Status = JobExecutionStatus.Completed,
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            EndTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(1)
        };

        context.JobExecutions.Add(execution);
        context.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(),
            JobExecutionId = execution.Id,
            Timestamp = DateTime.UtcNow.AddYears(-5),
            Level = LogLevel.Warning,
            Message = "Very old warning"
        });

        await context.SaveChangesAsync();

        var settingsService = new Mock<IArchiveApplicationSettingsService>();
        settingsService
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArchiveApplicationSettings
            {
                LogRetentionValue = 0,
                LogRetentionUnit = LogRetentionUnit.Months
            });

        var retentionService = new ExecutionLogRetentionService(context, settingsService.Object);
        await retentionService.PruneAsync();

        var count = await context.ExecutionLogs.AsNoTracking().CountAsync();
        Assert.Equal(1, count);
    }
}
