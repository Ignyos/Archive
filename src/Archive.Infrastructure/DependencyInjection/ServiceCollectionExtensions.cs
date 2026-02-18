using Archive.Core.Jobs;
using Archive.Infrastructure.Configuration;
using Archive.Infrastructure.Jobs;
using Archive.Infrastructure.Persistence;
using Archive.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Archive.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Archive");

        var connectionStringResolver = new SqliteConnectionStringResolver(appDataDirectory);
        var connectionString = connectionStringResolver.Resolve(configuration.GetConnectionString("ArchiveDb"));

        services.AddDbContext<ArchiveDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IJobExecutionService, JobExecutionService>();
        services.AddScoped<IJobSchedulerService, JobSchedulerService>();
        services.AddScoped<IBackupJobStateService, BackupJobStateService>();

        services.AddQuartz(q =>
        {
            q.UsePersistentStore(storeOptions =>
            {
                storeOptions.UseProperties = true;
                storeOptions.UseBinarySerializer();
                storeOptions.UseMicrosoftSQLite(connectionString);
            });
        });

        services.AddSingleton(provider =>
        {
            QuartzSchemaInitializer.EnsureCreated(connectionString);

            return provider
                .GetRequiredService<ISchedulerFactory>()
                .GetScheduler()
                .GetAwaiter()
                .GetResult();
        });

        return services;
    }
}
