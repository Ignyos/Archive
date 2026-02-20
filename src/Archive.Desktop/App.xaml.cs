using System.Windows;
using System.Threading;
using Archive.Core.Configuration;
using Archive.Core.Jobs;
using Archive.Desktop.Logging;
using Archive.Infrastructure.DependencyInjection;
using Archive.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace Archive.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private static Mutex? _singleInstanceMutex;

	public static AppSettings Settings { get; private set; } = new();

	public static IServiceProvider Services { get; private set; } = default!;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		const string instanceMutexName = "Global\\Archive.Desktop.SingleInstance";
		_singleInstanceMutex = new Mutex(initiallyOwned: true, instanceMutexName, out var createdNew);
		if (!createdNew)
		{
			System.Windows.MessageBox.Show(
				"Archive is already running. Check your system tray for the existing instance.",
				"Archive",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
			Shutdown();
			return;
		}

		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appSettings.json", optional: false, reloadOnChange: false)
			.Build();

		Settings = configuration.Get<AppSettings>() ?? new AppSettings();

		Log.Logger = new LoggerConfiguration().CreateLogger();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddSingleton(Settings);
		services.AddLogging(builder => builder.AddSerilog());
		services.AddInfrastructure(configuration);
		services.AddSingleton<MainWindow>();

		Services = services.BuildServiceProvider();

		using (var startupScope = Services.CreateScope())
		{
			var dbContext = startupScope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
			if (!ApplyMigrationsWithRetry(dbContext))
			{
				System.Windows.MessageBox.Show(
					"Archive could not initialize the local database because it is locked by another process. Close other Archive instances and try again.",
					"Archive",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				Shutdown();
				return;
			}

			try
			{
				var retentionService = startupScope.ServiceProvider.GetRequiredService<IExecutionLogRetentionService>();
				retentionService.PruneAsync().GetAwaiter().GetResult();
			}
			catch
			{
			}

			var scheduleControlService = startupScope.ServiceProvider.GetRequiredService<IArchiveScheduleControlService>();
			scheduleControlService.InitializeAsync().GetAwaiter().GetResult();
		}

		var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
		Log.Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(configuration)
			.WriteTo.Sink(new DatabaseLogSink(scopeFactory))
			.CreateLogger();

		var mainWindow = Services.GetRequiredService<MainWindow>();
		mainWindow.Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (Services is not null)
		{
			var scheduler = Services.GetService<IScheduler>();
			scheduler?.Shutdown(waitForJobsToComplete: false).GetAwaiter().GetResult();
		}

		Log.CloseAndFlush();

		try
		{
			_singleInstanceMutex?.ReleaseMutex();
		}
		catch
		{
		}

		_singleInstanceMutex?.Dispose();
		_singleInstanceMutex = null;

		base.OnExit(e);
	}

	private static bool ApplyMigrationsWithRetry(ArchiveDbContext dbContext)
	{
		const int maxAttempts = 5;

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				dbContext.Database.Migrate();
				return true;
			}
			catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < maxAttempts)
			{
				Thread.Sleep(250 * attempt);
			}
			catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
			{
				return false;
			}
		}

		return false;
	}
}

