using Archive.Core.Domain.Entities;
using Archive.Core.Domain.Enums;
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

    public DbSet<CredentialProfile> CredentialProfiles => Set<CredentialProfile>();

    public DbSet<AzureSqlBackupJob> AzureSqlBackupJobs => Set<AzureSqlBackupJob>();

    public DbSet<AzureSqlBackupDestination> AzureSqlBackupDestinations => Set<AzureSqlBackupDestination>();

    public DbSet<AzureSqlBackupWorkflowAction> AzureSqlBackupWorkflowActions => Set<AzureSqlBackupWorkflowAction>();

    public DbSet<ApplicationLog> ApplicationLogs => Set<ApplicationLog>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>()
            .HasKey(x => x.Key);

        modelBuilder.Entity<ApplicationLog>()
            .HasIndex(x => x.TimestampUtc);

        modelBuilder.Entity<CredentialProfile>()
            .HasIndex(x => new { x.ProviderType, x.Name })
            .IsUnique();

        modelBuilder.Entity<BackupJob>()
            .HasMany(x => x.Executions)
            .WithOne(x => x.Job)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BackupJob>()
            .Property(x => x.JobType)
            .HasDefaultValue(JobType.DirectorySync);

        modelBuilder.Entity<AzureSqlBackupJob>()
            .HasKey(x => x.JobId);

        modelBuilder.Entity<BackupJob>()
            .HasOne(x => x.AzureSqlBackupJob)
            .WithOne(x => x.Job)
            .HasForeignKey<AzureSqlBackupJob>(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AzureSqlBackupDestination>()
            .HasOne(x => x.AzureSqlBackupJob)
            .WithMany(x => x.Destinations)
            .HasForeignKey(x => x.AzureSqlBackupJobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AzureSqlBackupWorkflowAction>()
            .HasOne(x => x.AzureSqlBackupJob)
            .WithMany(x => x.WorkflowActions)
            .HasForeignKey(x => x.AzureSqlBackupJobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AzureSqlBackupWorkflowAction>()
            .HasOne(x => x.AzureSqlBackupDestination)
            .WithMany()
            .HasForeignKey(x => x.AzureSqlBackupDestinationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AzureSqlBackupWorkflowAction>()
            .HasIndex(x => new { x.AzureSqlBackupJobId, x.StepOrder })
            .IsUnique();

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
