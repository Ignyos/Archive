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

    public ObservableCollection<ExecutionLogRow> ExecutionLogItems { get; } = [];

    public JobHistoryWindow(JobListItemViewModel selectedJob)
    {
        InitializeComponent();
        ExecutionsDataGrid.ItemsSource = ExecutionItems;
        ExecutionLogsDataGrid.ItemsSource = ExecutionLogItems;
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
        ExecutionLogItems.Clear();

        SelectedExecutionSummaryTextBlock.Text = "Select an execution above to inspect details.";
        SelectedExecutionCountersTextBlock.Text = string.Empty;

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

    private void ExecutionsDataGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ExecutionsDataGrid.SelectedItem is not ExecutionHistoryRow selected)
        {
            ExecutionLogItems.Clear();
            SelectedExecutionSummaryTextBlock.Text = "Select an execution above to inspect details.";
            SelectedExecutionCountersTextBlock.Text = string.Empty;
            return;
        }

        SelectedExecutionSummaryTextBlock.Text =
            $"Status: {selected.Status}  |  Start: {selected.StartTimeLocal}  |  End: {selected.EndTimeLocal}  |  Duration: {selected.DurationText}";

        SelectedExecutionCountersTextBlock.Text =
            $"Scanned: {selected.FilesScanned}  Copied: {selected.FilesCopied}  Updated: {selected.FilesUpdated}  Deleted: {selected.FilesDeleted}  Skipped: {selected.FilesSkipped}  Failed: {selected.FilesFailed}  Warnings: {selected.WarningCount}  Errors: {selected.ErrorCount}  Bytes: {selected.BytesTransferred:N0}";

        LoadExecutionLogs(selected.Id);
    }

    private void LoadExecutionLogs(Guid executionId)
    {
        ExecutionLogItems.Clear();

        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

            var logs = dbContext.ExecutionLogs
                .AsNoTracking()
                .Where(x => x.JobExecutionId == executionId)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new ExecutionLogRow
                {
                    TimestampLocal = x.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    Level = x.Level.ToString(),
                    OperationType = x.OperationType.HasValue ? x.OperationType.Value.ToString() : string.Empty,
                    FilePath = x.FilePath ?? string.Empty,
                    Message = x.Message
                })
                .ToList();

            foreach (var log in logs)
            {
                ExecutionLogItems.Add(log);
            }
        }
        catch
        {
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

    private void OpenSelectedExecutionDetails()
    {
        if (ExecutionsDataGrid.SelectedItem is not ExecutionHistoryRow selected)
        {
            return;
        }

        if (_detailsWindow is not null)
        {
            _detailsWindow.LoadExecution(selected.Id, JobNameTextBlock.Text);
            _detailsWindow.Show();
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new JobExecutionDetailsWindow(selected.Id, JobNameTextBlock.Text)
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

    public sealed class ExecutionLogRow
    {
        public string TimestampLocal { get; init; } = string.Empty;

        public string Level { get; init; } = string.Empty;

        public string OperationType { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }
}