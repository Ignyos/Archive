using System.Collections.ObjectModel;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using Archive.Core.Domain.Enums;

namespace Archive.Desktop;

public partial class JobHistoryWindow : Window
{
    private Guid _jobId;
    private JobExecutionDetailsWindow? _detailsWindow;

    public ObservableCollection<ExecutionHistoryRow> ExecutionItems { get; } = [];

    public JobHistoryWindow(JobListItemViewModel selectedJob)
    {
        InitializeComponent();
        ExecutionsDataGrid.ItemsSource = ExecutionItems;
        LoadJob(selectedJob);
    }

    public void LoadJob(JobListItemViewModel selectedJob)
    {
        _jobId = selectedJob.Id;
        JobNameTextBlock.Text = selectedJob.Name;
        Title = $"Job History - {selectedJob.Name}";
        LoadHistory();
    }

    private void LoadHistory()
    {
        ExecutionItems.Clear();

        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

            var rows = dbContext.JobExecutions
                .AsNoTracking()
                .Where(x => x.JobId == _jobId)
                .OrderByDescending(x => x.StartTime)
                .Select(x => new ExecutionHistoryRow
                {
                    Id = x.Id,
                    StatusValue = x.Status,
                    StartTimeUtc = x.StartTime,
                    Duration = x.Duration,
                    FilesScanned = x.FilesScanned,
                    FilesCopied = x.FilesCopied,
                    FilesUpdated = x.FilesUpdated,
                    FilesDeleted = x.FilesDeleted,
                    FilesFailed = x.FilesFailed,
                    WarningCount = x.WarningCount,
                    ErrorCount = x.ErrorCount,
                    EndTimeUtc = x.EndTime,
                    FilesSkipped = x.FilesSkipped,
                    BytesTransferred = x.BytesTransferred
                })
                .ToList();

            foreach (var row in rows)
            {
                ExecutionItems.Add(row);
            }

            EmptyStateTextBlock.Visibility = rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (rows.Count > 0)
            {
                ExecutionsDataGrid.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            EmptyStateTextBlock.Visibility = Visibility.Visible;
            EmptyStateTextBlock.Text = $"Unable to load execution history. {ex.Message}";
        }
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadHistory();
    }

    private void OpenDetailsButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedExecutionDetails();
    }

    private void ExecutionsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedExecutionDetails();
    }

    private void ExecutionsDataGrid_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        OpenSelectedExecutionDetails();
    }

    private void ViewDetailsHyperlink_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkContentElement element || element.DataContext is not ExecutionHistoryRow row)
        {
            return;
        }

        OpenExecutionDetails(row.Id);
    }

    private void OpenSelectedExecutionDetails()
    {
        if (ExecutionsDataGrid.SelectedItem is not ExecutionHistoryRow selected)
        {
            return;
        }

        OpenExecutionDetails(selected.Id);
    }

    private void OpenExecutionDetails(Guid executionId)
    {
        var jobName = JobNameTextBlock.Text;

        if (_detailsWindow is not null)
        {
            _detailsWindow.LoadExecution(executionId, jobName);
            _detailsWindow.Show();
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new JobExecutionDetailsWindow(executionId, jobName)
        {
            Owner = this
        };

        _detailsWindow.Closed += (_, _) => _detailsWindow = null;
        _detailsWindow.Show();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public sealed class ExecutionHistoryRow
    {
        public Guid Id { get; init; }

        public DateTime StartTimeUtc { get; init; }

        public DateTime? EndTimeUtc { get; init; }

        public TimeSpan? Duration { get; init; }

        public JobExecutionStatus StatusValue { get; init; }

        public string StartTimeLocal => ExecutionDisplayFormatter.FormatTimestamp(StartTimeUtc);

        public string EndTimeLocal => ExecutionDisplayFormatter.FormatTimestamp(EndTimeUtc);

        public string Status => ExecutionDisplayFormatter.FormatStatus(StatusValue, WarningCount, ErrorCount, FilesFailed);

        public string DurationText => ExecutionDisplayFormatter.FormatDuration(Duration);

        public int FilesScanned { get; init; }

        public int FilesCopied { get; init; }

        public int FilesUpdated { get; init; }

        public int FilesDeleted { get; init; }

        public int FilesSkipped { get; init; }

        public int FilesFailed { get; init; }

        public int WarningCount { get; init; }

        public int ErrorCount { get; init; }

        public long BytesTransferred { get; init; }
    }
}