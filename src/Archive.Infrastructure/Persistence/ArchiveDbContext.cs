using Archive.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Archive.Infrastructure.Persistence;

public sealed class ArchiveDbContext : DbContext
{
    public ArchiveDbContext(DbContextOptions<ArchiveDbContext> options) : base(options)
    {
    }

    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();

    public DbSet<SyncOptions> SyncOptions => Set<SyncOptions>();

    public DbSet<ExclusionPattern> ExclusionPatterns => Set<ExclusionPattern>();

    public DbSet<BackupJobExclusionPattern> BackupJobExclusionPatterns => Set<BackupJobExclusionPattern>();

    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>()
            .HasKey(x => x.Key);

        modelBuilder.Entity<BackupJob>()
            .HasMany(x => x.Executions)
            .WithOne(x => x.Job)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobExecution>()
            .HasMany(x => x.Logs)
            .WithOne(x => x.Execution)
            .HasForeignKey(x => x.JobExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BackupJob>()
            .HasMany(x => x.BackupJobExclusionPatterns)
            .WithOne(x => x.BackupJob)
            .HasForeignKey(x => x.BackupJobId);

        modelBuilder.Entity<ExclusionPattern>()
            .HasMany(x => x.BackupJobExclusionPatterns)
            .WithOne(x => x.ExclusionPattern)
            .HasForeignKey(x => x.ExclusionPatternId);

        modelBuilder.Entity<BackupJobExclusionPattern>()
            .HasKey(x => new { x.BackupJobId, x.ExclusionPatternId });
    }
}
