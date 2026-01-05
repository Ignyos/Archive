using System.IO;
using System.Windows;
using Archive.Core;
using Archive.GUI.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace Archive.GUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private DatabaseService? _databaseService;
    private SchedulerService? _schedulerService;
    private MainWindow? _mainWindow;
    private Dictionary<string, (BackupJob Job, SyncResult Result)> _recentCompletions = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize services
        _databaseService = new DatabaseService();
        
        // Migrate from JSON if it exists
        var jsonPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Archive", "jobs.json");
        _ = _databaseService.MigrateFromJsonAsync(jsonPath);
        
        _schedulerService = new SchedulerService(_databaseService);

        // Subscribe to scheduler events
        _schedulerService.JobCompleted += OnJobCompleted;
        _schedulerService.StatusChanged += OnStatusChanged;

        // Start scheduler
        _ = _schedulerService.StartAsync();

        // Get the TaskbarIcon from resources
        _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
        
        // Subscribe to balloon click events
        if (_notifyIcon != null)
        {
            _notifyIcon.TrayBalloonTipClicked += OnBalloonTipClicked;
        }

        // Update startup menu item to reflect current state
        UpdateStartupMenuState();

        // Check and prompt for startup configuration, and show window on first run
        _ = CheckStartupConfigurationAsync();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Don't show main window on startup, just system tray
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _schedulerService?.Stop();
        _notifyIcon?.Dispose();
    }

    private void NotifyIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuToggleScheduler_Click(object sender, RoutedEventArgs e)
    {
        if (_schedulerService == null)
            return;

        var menuItem = sender as System.Windows.Controls.MenuItem;
        if (menuItem?.IsChecked == true)
        {
            _ = _schedulerService.StartAsync();
        }
        else
        {
            _schedulerService.Stop();
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        // Check if any jobs are running
        if (HasRunningJobs())
        {
            var result = MessageBox.Show(
                "One or more backup jobs are currently running. Are you sure you want to exit?\n\nRunning jobs will be stopped.",
                "Jobs Running",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        Shutdown();
    }

    private bool HasRunningJobs()
    {
        if (_databaseService == null)
            return false;

        var collection = _databaseService.LoadJobsAsync().GetAwaiter().GetResult();
        return collection.Jobs.Any(job => job.IsRunning);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow(_databaseService!, _schedulerService!);
            _mainWindow.Closed += (s, e) => _mainWindow = null;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnJobCompleted(object? sender, JobCompletedEventArgs e)
    {
        // Store the result for report viewing
        _recentCompletions[e.Job.Id] = (e.Job, e.Result);
    }

    private void OnBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        // Show the most recent job report
        if (_recentCompletions.Count > 0)
        {
            var lastCompletion = _recentCompletions.Values.Last();
            ShowJobReport(lastCompletion.Job, lastCompletion.Result);
        }
    }

    private void ShowJobReport(BackupJob job, SyncResult result)
    {
        Dispatcher.Invoke(() =>
        {
            var reportWindow = new JobReportWindow(job, result)
            {
                Owner = _mainWindow
            };
            reportWindow.Show();
            reportWindow.Activate();
        });
    }

    private void OnStatusChanged(object? sender, string status)
    {
        // Could update tooltip or show status in UI
    }

    private async Task CheckStartupConfigurationAsync()
    {
        if (_databaseService == null)
            return;

        var collection = await _databaseService.LoadJobsAsync();
        var isFirstRun = !collection.HasLaunchedBefore;

        // Show main window and enable startup on first run
        if (isFirstRun)
        {
            ShowMainWindow();
            
            // Automatically enable startup on first run
            StartupManager.SetRunOnStartup(true);

            collection.HasLaunchedBefore = true;
            await _databaseService.SaveJobsAsync(collection);
        }
    }

    private void MenuToggleStartup_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as System.Windows.Controls.MenuItem;
        if (menuItem == null)
            return;

        var isChecked = menuItem.IsChecked;
        var success = StartupManager.SetRunOnStartup(isChecked);

        if (!success)
        {
            MessageBox.Show(
                "Failed to update Windows startup settings. Please check your permissions.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Revert the check state
            menuItem.IsChecked = !isChecked;
        }
    }

    private void UpdateStartupMenuState()
    {
        if (_notifyIcon?.ContextMenu != null)
        {
            var menuItem = _notifyIcon.ContextMenu.Items
                .OfType<System.Windows.Controls.MenuItem>()
                .FirstOrDefault(m => m.Name == "MenuStartupStatus");

            if (menuItem != null)
            {
                menuItem.IsChecked = StartupManager.IsSetToRunOnStartup();
            }
        }
    }
}
