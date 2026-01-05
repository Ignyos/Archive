using System.Windows;
using System.Windows.Documents;
using Archive.Core;
using Archive.GUI.Services;

namespace Archive.GUI;

/// <summary>
/// Interaction logic for JobHistoryWindow.xaml
/// </summary>
public partial class JobHistoryWindow : Window
{
    private readonly DatabaseService _databaseService;
    private readonly BackupJob _job;

    public JobHistoryWindow(DatabaseService databaseService, BackupJob job)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _job = job;
        
        Loaded += JobHistoryWindow_Loaded;
    }

    private async void JobHistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            TxtJobName.Text = _job.Name;
            TxtSourcePath.Text = $"Source: {_job.SourcePath}";
            TxtBackupPath.Text = $"Backup: {_job.DestinationPath}";

            var history = await _databaseService.GetJobHistoryAsync(_job.Id);
            HistoryDataGrid.ItemsSource = history.Select(h => new
            {
                HistoryId = h.Id,
                StartTime = h.StartTime,
                Duration = FormatDuration(h.Result.Duration),
                Success = h.Result.Success,
                FilesCopied = h.Result.FilesCopied,
                FilesUpdated = h.Result.FilesUpdated,
                FilesDeleted = h.Result.FilesDeleted,
                FilesFailed = h.Result.Errors.Count
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private async void HistoryDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (HistoryDataGrid.SelectedItem != null)
        {
            // Get the selected history entry
            dynamic historyEntry = HistoryDataGrid.SelectedItem;
            await ShowHistoryDetailsAsync(historyEntry);
        }
    }

    private async void HistoryDetails_Click(object sender, RoutedEventArgs e)
    {
        // Get the history entry from the hyperlink's Tag
        if (sender is Hyperlink hyperlink && hyperlink.Tag != null)
        {
            dynamic historyEntry = hyperlink.Tag;
            await ShowHistoryDetailsAsync(historyEntry);
        }
    }

    private async Task ShowHistoryDetailsAsync(dynamic historyEntry)
    {
        long historyId = historyEntry.HistoryId;
        
        try
            {
                // Retrieve the operation logs from the database
                var logs = await _databaseService.GetJobLogsAsync(historyId);
                
                if (logs.Count == 0)
                {
                    MessageBox.Show("No detailed logs available for this run.", "Job Details", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // Create a window to display the logs
                var logsWindow = new Window
                {
                    Title = $"Job Details - {_job.Name} - {historyEntry.StartTime:g}",
                    Width = 900,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                
                // Create main grid
                var mainGrid = new System.Windows.Controls.Grid
                {
                    Margin = new Thickness(10)
                };
                
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                
                // Summary section
                var summaryPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                summaryPanel.Children.Add(new System.Windows.Controls.TextBlock 
                { 
                    Text = $"Job: {_job.Name}", 
                    FontWeight = FontWeights.Bold, 
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                summaryPanel.Children.Add(new System.Windows.Controls.TextBlock 
                { 
                    Text = $"Run Time: {historyEntry.StartTime:g}  |  Duration: {historyEntry.Duration}  |  Status: {(historyEntry.Success ? "Success" : "Failed")}",
                    Margin = new Thickness(0, 0, 0, 5)
                });
                summaryPanel.Children.Add(new System.Windows.Controls.TextBlock 
                { 
                    Text = $"Copied: {historyEntry.FilesCopied}  |  Updated: {historyEntry.FilesUpdated}  |  Deleted: {historyEntry.FilesDeleted}  |  Failed: {historyEntry.FilesFailed}",
                    Margin = new Thickness(0, 0, 0, 5)
                });
                
                System.Windows.Controls.Grid.SetRow(summaryPanel, 0);
                mainGrid.Children.Add(summaryPanel);
                
                // Scrollable operations section
                var scrollViewer = new System.Windows.Controls.ScrollViewer
                {
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
                };
                
                var operationsPanel = new System.Windows.Controls.StackPanel();
                
                // Group logs by type
                var groupedLogs = logs.GroupBy(l => l.Level).OrderBy(g => GetOperationOrder(g.Key));
                
                foreach (var group in groupedLogs)
                {
                    var expander = new System.Windows.Controls.Expander
                    {
                        Header = $"{group.Key} Operations ({group.Count()})",
                        Margin = new Thickness(0, 0, 0, 5),
                        IsExpanded = group.Key == "Error" // Expand errors by default
                    };
                    
                    // Set header style
                    var headerStyle = new Style(typeof(System.Windows.Controls.Expander));
                    headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, FontWeights.Bold));
                    if (group.Key == "Error")
                    {
                        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, System.Windows.Media.Brushes.Red));
                    }
                    expander.Style = headerStyle;
                    
                    // Create list of operations
                    var listBox = new System.Windows.Controls.ListBox
                    {
                        BorderThickness = new Thickness(0),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 11,
                        Background = System.Windows.Media.Brushes.Transparent
                    };
                    
                    foreach (var log in group)
                    {
                        var item = new System.Windows.Controls.TextBlock
                        {
                            Text = $"[{log.Timestamp:HH:mm:ss}] {log.Message}",
                            TextWrapping = TextWrapping.NoWrap,
                            Margin = new Thickness(5, 2, 5, 2)
                        };
                        
                        if (group.Key == "Error")
                        {
                            item.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        
                        listBox.Items.Add(item);
                    }
                    
                    expander.Content = listBox;
                    operationsPanel.Children.Add(expander);
                }
                
                scrollViewer.Content = operationsPanel;
                System.Windows.Controls.Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);
                
                logsWindow.Content = mainGrid;
                logsWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading detailed logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private int GetOperationOrder(string operationType)
    {
        return operationType switch
        {
            "Copy" => 1,
            "Update" => 2,
            "Delete" => 3,
            "Skip" => 4,
            "Error" => 5,
            _ => 99
        };
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
