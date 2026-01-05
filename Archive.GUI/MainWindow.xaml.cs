using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Archive.Core;
using Archive.GUI.Services;

namespace Archive.GUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DatabaseService _databaseService;
    private readonly SchedulerService _schedulerService;
    private BackupJobCollection _collection;
    private UpdateInfo? _availableUpdate;

    public MainWindow(DatabaseService databaseService, SchedulerService schedulerService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _schedulerService = schedulerService;
        _collection = new BackupJobCollection();

        // Subscribe to scheduler events
        _schedulerService.JobStarted += OnJobStarted;
        _schedulerService.JobCompleted += OnJobCompleted;
        _schedulerService.UpdateAvailable += OnUpdateAvailable;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Check if any jobs are running
        if (_collection.Jobs.Any(job => job.IsRunning))
        {
            var result = MessageBox.Show(
                "One or more backup jobs are currently running. Closing this window will not stop them.\n\nWould you like to keep the window open?",
                "Jobs Running",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        // Prevent window from closing, just hide it instead
        e.Cancel = true;
        Hide();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadJobsAsync();
        StatusBarText.Text = " You're on the most recent version: v1.0.0";
    }

    private async Task LoadJobsAsync()
    {
        try
        {
            _collection = await _databaseService.LoadJobsAsync();
            JobsDataGrid.ItemsSource = _collection.Jobs;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading jobs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveJobsAsync()
    {
        try
        {
            await _databaseService.SaveJobsAsync(_collection);
            await _schedulerService.ReloadJobsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving jobs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void JobsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection changed - context menu will handle actions
    }

    private void JobsDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        bool hasSelection = JobsDataGrid.SelectedItem != null;
        var selectedJob = JobsDataGrid.SelectedItem as BackupJob;
        bool isJobRunning = selectedJob?.IsRunning ?? false;
        
        // Show "Add Job" only when no item is selected
        MenuAddJob.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        
        // Show job-specific items only when an item is selected
        MenuEditJob.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        MenuDeleteJob.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        MenuDeleteJob.IsEnabled = !isJobRunning; // Disable delete when job is running
        MenuSeparator1.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        
        // Always show Run Now and Stop Job when a job is selected, but enable/disable based on running state
        MenuRunJob.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        MenuRunJob.IsEnabled = !isJobRunning;
        
        MenuStopJob.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        MenuStopJob.IsEnabled = isJobRunning;
        
        MenuSeparator2.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        MenuViewHistory.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
    }

    private void JobsDataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            JobsDataGrid.SelectedItem = null;
            e.Handled = true;
        }
    }

    private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // If click is outside the DataGrid rows, deselect
        var element = e.OriginalSource as DependencyObject;
        
        // Walk up the visual tree to check if we clicked on a DataGridRow
        while (element != null && element != JobsDataGrid)
        {
            if (element is DataGridRow)
            {
                // Clicked on a row, don't deselect
                return;
            }
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        
        // Clicked outside any row, deselect
        JobsDataGrid.SelectedItem = null;
    }

    private void JobsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (JobsDataGrid.SelectedItem != null)
        {
            BtnEditJob_Click(sender, e);
        }
    }

    private void BtnAddJob_Click(object sender, RoutedEventArgs e)
    {
        var newJob = new BackupJob
        {
            Name = "New Backup Job",
            SourcePath = "",
            DestinationPath = "",
            Enabled = true
        };

        var dialog = new JobEditWindow(newJob);
        if (dialog.ShowDialog() == true)
        {
            _collection.Jobs.Add(newJob);
            JobsDataGrid.Items.Refresh();
            _ = SaveJobsAsync();
        }
    }

    private void BtnEditJob_Click(object sender, RoutedEventArgs e)
    {
        if (JobsDataGrid.SelectedItem is not BackupJob selectedJob)
            return;

        // Cancel any edit transaction before opening dialog
        JobsDataGrid.CommitEdit();
        JobsDataGrid.CancelEdit();

        var dialog = new JobEditWindow(selectedJob);
        if (dialog.ShowDialog() == true)
        {
            JobsDataGrid.Items.Refresh();
            _ = SaveJobsAsync();
        }
    }

    private async void BtnDeleteJob_Click(object sender, RoutedEventArgs e)
    {
        if (JobsDataGrid.SelectedItem is not BackupJob selectedJob)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the job '{selectedJob.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _collection.Jobs.Remove(selectedJob);
            JobsDataGrid.Items.Refresh();
            await SaveJobsAsync();
        }
    }

    private async void BtnRunJob_Click(object sender, RoutedEventArgs e)
    {
        if (JobsDataGrid.SelectedItem is not BackupJob selectedJob)
            return;

        await _schedulerService.RunJobAsync(selectedJob);
        JobsDataGrid.Items.Refresh();
    }

    private void BtnStopJob_Click(object sender, RoutedEventArgs e)
    {
        if (JobsDataGrid.SelectedItem is not BackupJob selectedJob || !selectedJob.IsRunning)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to stop the job '{selectedJob.Name}'?",
            "Stop Job",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _schedulerService.CancelJob(selectedJob);
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadJobsAsync();
    }

    private void OnJobStarted(object? sender, JobStartedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Cancel any edit transaction before refreshing
            JobsDataGrid.CommitEdit();
            JobsDataGrid.CancelEdit();
            JobsDataGrid.Items.Refresh();
        });
    }

    private void OnJobCompleted(object? sender, JobCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Cancel any edit transaction before refreshing
            JobsDataGrid.CommitEdit();
            JobsDataGrid.CancelEdit();
            JobsDataGrid.Items.Refresh();
        });
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_databaseService)
        {
            Owner = this
        };
        settingsWindow.ShowDialog();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        // Create a custom about dialog with a clickable link
        var aboutWindow = new Window
        {
            Title = "About Archive",
            Width = 450,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var stackPanel = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Title
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Archive - Backup Manager",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 10, 0, 15),
            TextAlignment = TextAlignment.Center
        });

        // Description
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "A simple and reliable backup utility for Windows.",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 20),
            TextAlignment = TextAlignment.Center
        });

        // Version
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Version 1.0",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 5),
            TextAlignment = TextAlignment.Center
        });

        // Copyright
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "© 2026",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 20),
            TextAlignment = TextAlignment.Center
        });

        // Website link
        var websiteLinkBlock = new System.Windows.Controls.TextBlock
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };
        var websiteLink = new Hyperlink
        {
            NavigateUri = new Uri("https://ignyos.com/"),
            Inlines = { new Run("https://ignyos.com/") }
        };
        websiteLink.RequestNavigate += (s, args) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = args.Uri.ToString(),
                UseShellExecute = true
            });
            args.Handled = true;
        };
        websiteLinkBlock.Inlines.Add(websiteLink);
        stackPanel.Children.Add(websiteLinkBlock);

        // Icon credit
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Icon credit: Server Rack Vectors by Vecteezy",
            FontSize = 10,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 5),
            TextAlignment = TextAlignment.Center
        });

        var iconLinkBlock = new System.Windows.Controls.TextBlock
        {
            TextAlignment = TextAlignment.Center
        };
        var iconLink = new Hyperlink
        {
            NavigateUri = new Uri("https://www.vecteezy.com/free-vector/server-rack"),
            Inlines = { new Run("https://www.vecteezy.com/free-vector/server-rack") },
            FontSize = 10
        };
        iconLink.RequestNavigate += (s, args) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = args.Uri.ToString(),
                UseShellExecute = true
            });
            args.Handled = true;
        };
        iconLinkBlock.Inlines.Add(iconLink);
        stackPanel.Children.Add(iconLinkBlock);

        aboutWindow.Content = stackPanel;
        aboutWindow.ShowDialog();
    }

    private void BtnViewHistory_Click(object sender, RoutedEventArgs e)
    {
        BackupJob? job = null;
        
        // Handle both Button (old) and Hyperlink (new) sources
        if (sender is Button button && button.Tag is BackupJob buttonJob)
        {
            job = buttonJob;
        }
        else if (sender is Hyperlink hyperlink && hyperlink.Tag is BackupJob hyperlinkJob)
        {
            job = hyperlinkJob;
        }

        if (job != null)
        {
            var historyWindow = new JobHistoryWindow(_databaseService, job)
            {
                Owner = this
            };
            historyWindow.Show();
        }
    }

    private void ContextMenu_ViewHistory_Click(object sender, RoutedEventArgs e)
    {
        if (JobsDataGrid.SelectedItem is BackupJob job)
        {
            var historyWindow = new JobHistoryWindow(_databaseService, job)
            {
                Owner = this
            };
            historyWindow.Show();
        }
    }

    private void OnUpdateAvailable(object? sender, UpdateInfo updateInfo)
    {
        _availableUpdate = updateInfo;
        
        Dispatcher.Invoke(() =>
        {
            StatusBarText.Text = $" A new version is available: v{updateInfo.Version}";
            UpdateButtonItem.Visibility = Visibility.Visible;
        });
    }

    private void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate != null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _availableUpdate.ReleaseUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open release page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}