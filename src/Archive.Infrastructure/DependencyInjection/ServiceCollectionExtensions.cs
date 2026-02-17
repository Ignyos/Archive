using Archive.Core.Jobs;
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
        var connectionString = configuration.GetConnectionString("ArchiveDb")
            ?? "Data Source=archive.db";

        services.AddDbContext<ArchiveDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<IJobExecutionService, JobExecutionService>();
        services.AddSingleton<IJobSchedulerService, JobSchedulerService>();

        services.AddQuartz(q =>
        {
            q.UsePersistentStore(storeOptions =>
            {
                storeOptions.UseProperties = true;
                storeOptions.UseSQLite(connectionString);
            });
        });

        services.AddSingleton(provider =>
            provider.GetRequiredService<ISchedulerFactory>().GetScheduler().GetAwaiter().GetResult());

        return services;
    }
}
