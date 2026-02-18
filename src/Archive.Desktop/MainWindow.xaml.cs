using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Reflection;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.Matchers;

namespace Archive.Desktop;

public partial class MainWindow : Window
{
    private const string JobKeyPrefix = "archive-job-";

    private readonly MainWindowCloseCoordinator _closeCoordinator = new();
    private readonly DispatcherTimer _runtimeRefreshTimer;
    private JobHistoryWindow? _jobHistoryWindow;
    private NotifyIcon? _notifyIcon;

    public ObservableCollection<JobListItemViewModel> JobItems { get; } = [];

    public JobListItemViewModel? SelectedJob { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        InitializeStatusBarText();
        InitializeTrayIcon();
        _runtimeRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _runtimeRefreshTimer.Tick += (_, _) => RefreshJobList();
        RefreshJobList();
        _runtimeRefreshTimer.Start();

        CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (_, _) => NewJobMenuItem_OnClick(this, new RoutedEventArgs())));
        InputBindings.Add(new KeyBinding(ApplicationCommands.New, new KeyGesture(Key.N, ModifierKeys.Control)));
    }

    private void InitializeStatusBarText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";

        VersionStatusTextBlock.Text = $"You're on the most recent version: v{versionText}";
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Archive",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Archive", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _closeCoordinator.RequestApplicationShutdown();
        Close();
    }

    private void NewJobMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("New Job dialog will be implemented in Phase 4.2.", "Archive", MessageBoxButton.OK, MessageBoxImage.Information);
        RefreshJobList();
    }

    private void SettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Settings window will be implemented in Phase 4.5.", "Archive", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Archive - Backup Manager", "About Archive", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeCoordinator.GetCloseAction() == MainWindowCloseAction.CloseApplication)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _runtimeRefreshTimer.Stop();

        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void RefreshJobList()
    {
        JobItems.Clear();

        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var scheduler = scope.ServiceProvider.GetService<IScheduler>();

            var runtimeSnapshot = BuildRuntimeSnapshot(scheduler);

            var rows = dbContext.BackupJobs
                .AsNoTracking()
                .Where(job => job.DeletedAt == null)
                .Select(job => new
                {
                    job.Id,
                    job.Name,
                    job.Description,
                    job.SourcePath,
                    job.DestinationPath,
                    job.Enabled,
                    job.TriggerType,
                    LatestExecutionStatus = dbContext.JobExecutions
                        .Where(execution => execution.JobId == job.Id)
                        .OrderByDescending(execution => execution.StartTime)
                        .Select(execution => (JobExecutionStatus?)execution.Status)
                        .FirstOrDefault()
                })
                .OrderBy(job => job.Name)
                .ToList();

            foreach (var row in rows)
            {
                var isCurrentlyRunning = runtimeSnapshot.RunningJobIds.Contains(row.Id);
                var nextRun = runtimeSnapshot.NextRunByJobId.GetValueOrDefault(row.Id);

                JobItems.Add(new JobListItemViewModel
                {
                    Id = row.Id,
                    Status = JobListStatusResolver.Resolve(
                        row.Enabled,
                        row.TriggerType,
                        row.LatestExecutionStatus,
                        isCurrentlyRunning),
                    Enabled = row.Enabled,
                    Name = string.IsNullOrWhiteSpace(row.Name) ? "(unnamed)" : row.Name,
                    Description = row.Description,
                    SourcePath = row.SourcePath,
                    DestinationPath = row.DestinationPath,
                    NextRun = row.Enabled && row.TriggerType != TriggerType.Manual
                        ? nextRun?.LocalDateTime
                        : null
                });
            }
        }
        catch
        {
            JobItems.Add(new JobListItemViewModel
            {
                Name = "Unable to load jobs",
                Description = "Check logs for details.",
                Status = "Error",
                Enabled = false,
                NextRun = null
            });
        }
    }

    private static (HashSet<Guid> RunningJobIds, Dictionary<Guid, DateTimeOffset?> NextRunByJobId) BuildRuntimeSnapshot(IScheduler? scheduler)
    {
        var runningJobIds = new HashSet<Guid>();
        var nextRunByJobId = new Dictionary<Guid, DateTimeOffset?>();

        if (scheduler is null)
        {
            return (runningJobIds, nextRunByJobId);
        }

        try
        {
            var executingJobs = scheduler.GetCurrentlyExecutingJobs().GetAwaiter().GetResult();
            foreach (var executingJob in executingJobs)
            {
                var jobId = TryParseJobId(executingJob.JobDetail.Key.Name);
                if (jobId.HasValue)
                {
                    runningJobIds.Add(jobId.Value);
                }
            }

            var jobKeys = scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).GetAwaiter().GetResult();
            foreach (var jobKey in jobKeys)
            {
                var jobId = TryParseJobId(jobKey.Name);
                if (!jobId.HasValue)
                {
                    continue;
                }

                var triggers = scheduler.GetTriggersOfJob(jobKey).GetAwaiter().GetResult();
                var nextFire = triggers
                    .Select(trigger => trigger.GetNextFireTimeUtc())
                    .Where(value => value.HasValue)
                    .Min();

                nextRunByJobId[jobId.Value] = nextFire;
            }
        }
        catch
        {
        }

        return (runningJobIds, nextRunByJobId);
    }

    private static Guid? TryParseJobId(string? jobKeyName)
    {
        if (string.IsNullOrWhiteSpace(jobKeyName) || !jobKeyName.StartsWith(JobKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = jobKeyName.Substring(JobKeyPrefix.Length);
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private JobListItemViewModel? GetSelectedJobFromSender(object sender)
    {
        if (sender is FrameworkElement element && element.DataContext is JobListItemViewModel row)
        {
            return row;
        }

        return SelectedJob;
    }

    private static string GetJobName(JobListItemViewModel? job)
    {
        return job?.Name ?? "(no job selected)";
    }

    private void ShowNotImplementedMessage(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenEditJob(JobListItemViewModel? selectedJob)
    {
        if (selectedJob is null)
        {
            ShowNotImplementedMessage("Archive", "Please select a job first.");
            return;
        }

        var editWindow = new JobEditWindow(selectedJob)
        {
            Owner = this
        };

        editWindow.ShowDialog();
        RefreshJobList();
    }

    private void OpenJobHistory(JobListItemViewModel? selectedJob)
    {
        if (selectedJob is null)
        {
            ShowNotImplementedMessage("Archive", "Please select a job first.");
            return;
        }

        if (_jobHistoryWindow is not null)
        {
            _jobHistoryWindow.LoadJob(selectedJob);
            _jobHistoryWindow.Show();
            _jobHistoryWindow.Activate();
            return;
        }

        _jobHistoryWindow = new JobHistoryWindow(selectedJob)
        {
            Owner = this
        };

        _jobHistoryWindow.Closed += (_, _) => _jobHistoryWindow = null;
        _jobHistoryWindow.Show();
    }

    private void EditJobMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedJob = GetSelectedJobFromSender(sender);
        OpenEditJob(selectedJob);
    }

    private void DeleteJobMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedJob = GetSelectedJobFromSender(sender);
        if (selectedJob is null)
        {
            ShowNotImplementedMessage("Archive", "Please select a job first.");
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            $"Delete job '{GetJobName(selectedJob)}'?\n\nThis performs a soft delete and removes the job from scheduling.",
            "Delete Job",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var stateService = scope.ServiceProvider.GetRequiredService<IBackupJobStateService>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<IJobSchedulerService>();

            var softDeleted = stateService.SoftDeleteAsync(selectedJob.Id).GetAwaiter().GetResult();
            if (!softDeleted)
            {
                ShowNotImplementedMessage("Archive", "Unable to delete job because it no longer exists.");
                RefreshJobList();
                return;
            }

            schedulerService.DeleteAsync(selectedJob.Id).GetAwaiter().GetResult();
            ShowNotImplementedMessage("Archive", $"Job deleted: {GetJobName(selectedJob)}");
        }
        catch
        {
            ShowNotImplementedMessage("Archive", "Unable to delete the selected job. Check logs for details.");
        }

        RefreshJobList();
    }

    private async void RunNowMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedJob = GetSelectedJobFromSender(sender);
        if (selectedJob is null)
        {
            ShowNotImplementedMessage("Archive", "Please select a job first.");
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var schedulerService = scope.ServiceProvider.GetRequiredService<IJobSchedulerService>();
            await schedulerService.RunNowAsync(selectedJob.Id);

            ShowNotImplementedMessage("Archive", $"Run Now requested for job: {GetJobName(selectedJob)}");
        }
        catch
        {
            ShowNotImplementedMessage("Archive", "Unable to run the selected job now. Check logs for details.");
        }

        RefreshJobList();
    }

    private async void StopJobMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedJob = GetSelectedJobFromSender(sender);
        if (selectedJob is null)
        {
            ShowNotImplementedMessage("Archive", "Please select a job first.");
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var schedulerService = scope.ServiceProvider.GetRequiredService<IJobSchedulerService>();
            var stopped = await schedulerService.StopAsync(selectedJob.Id);

            if (stopped)
            {
                ShowNotImplementedMessage("Archive", $"Stop requested for job: {GetJobName(selectedJob)}");
            }
            else
            {
                ShowNotImplementedMessage("Archive", $"Job is not currently interruptible: {GetJobName(selectedJob)}");
            }
        }
        catch
        {
            ShowNotImplementedMessage("Archive", "Unable to stop the selected job. Check logs for details.");
        }

        RefreshJobList();
    }

    private void ViewHistoryMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedJob = GetSelectedJobFromSender(sender);
        OpenJobHistory(selectedJob);
    }

    private void HistoryHyperlink_OnClick(object sender, RoutedEventArgs e)
    {
        ViewHistoryMenuItem_OnClick(sender, e);
    }

    private void JobsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject sourceElement)
        {
            var current = sourceElement;
            while (current is not null)
            {
                if (current is System.Windows.Controls.CheckBox)
                {
                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        if (SelectedJob is null)
        {
            return;
        }

        OpenEditJob(SelectedJob);
    }

    private void JobsDataGrid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        var dependencyObject = e.OriginalSource as DependencyObject;

        while (dependencyObject is not null && dependencyObject is not DataGridRow)
        {
            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        if (dependencyObject is DataGridRow row)
        {
            row.IsSelected = true;
            dataGrid.SelectedItem = row.Item;
            SelectedJob = row.Item as JobListItemViewModel;
        }
    }

    private async void JobsDataGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
        {
            return;
        }

        if (e.Row.Item is not JobListItemViewModel row)
        {
            return;
        }

        if (e.Column is not DataGridCheckBoxColumn checkBoxColumn)
        {
            return;
        }

        if (!string.Equals(checkBoxColumn.Header?.ToString(), "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (e.EditingElement is System.Windows.Controls.CheckBox checkBox)
        {
            row.Enabled = checkBox.IsChecked ?? false;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var backupJobStateService = scope.ServiceProvider.GetRequiredService<IBackupJobStateService>();

            var success = await backupJobStateService.SetEnabledAsync(row.Id, row.Enabled);

            if (!success)
            {
                ShowNotImplementedMessage("Archive", "Unable to update job enabled state because the job no longer exists.");
            }

            RefreshJobList();
        }
        catch
        {
            ShowNotImplementedMessage("Archive", "Unable to update the job enabled state. Check logs for details.");
            RefreshJobList();
        }
    }
}