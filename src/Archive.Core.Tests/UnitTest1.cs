using Archive.Core.Configuration;
using Archive.Core.Domain.Entities;
using Archive.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
