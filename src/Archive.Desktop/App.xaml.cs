using System.Windows;
using Archive.Core.Configuration;
using Archive.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;

namespace Archive.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	public static AppSettings Settings { get; private set; } = new();

	public static IServiceProvider Services { get; private set; } = default!;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appSettings.json", optional: false, reloadOnChange: false)
			.Build();

		Settings = configuration.Get<AppSettings>() ?? new AppSettings();

		Log.Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(configuration)
			.CreateLogger();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddSingleton(Settings);
		services.AddLogging(builder => builder.AddSerilog());
		services.AddInfrastructure(configuration);
		services.AddSingleton<MainWindow>();

		Services = services.BuildServiceProvider();

		if (Settings.Archive.ArchiveScheduleEnabled)
		{
			var scheduler = Services.GetRequiredService<IScheduler>();
			scheduler.Start().GetAwaiter().GetResult();
		}

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
		base.OnExit(e);
	}
}

