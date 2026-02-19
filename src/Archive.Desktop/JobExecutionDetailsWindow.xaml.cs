using System.Collections.ObjectModel;
using System.Windows;
using Archive.Core.Domain.Enums;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Archive.Desktop;

public partial class JobExecutionDetailsWindow : Window
{
    private Guid _executionId;

    public ObservableCollection<ExecutionLogRow> CopyLogs { get; } = [];
    public ObservableCollection<ExecutionLogRow> UpdateLogs { get; } = [];
    public ObservableCollection<ExecutionLogRow> DeleteLogs { get; } = [];
    public ObservableCollection<ExecutionLogRow> SkippedLogs { get; } = [];
    public ObservableCollection<ExecutionLogRow> FailedLogs { get; } = [];

    public JobExecutionDetailsWindow(Guid executionId, string jobName)
    {
        InitializeComponent();

        CopyLogsDataGrid.ItemsSource = CopyLogs;
        UpdateLogsDataGrid.ItemsSource = UpdateLogs;
        DeleteLogsDataGrid.ItemsSource = DeleteLogs;
        SkippedLogsDataGrid.ItemsSource = SkippedLogs;
        FailedLogsDataGrid.ItemsSource = FailedLogs;

        LoadExecution(executionId, jobName);
    }

    public void LoadExecution(Guid executionId, string jobName)
    {
        _executionId = executionId;

        CopyLogs.Clear();
        UpdateLogs.Clear();
        DeleteLogs.Clear();
        SkippedLogs.Clear();
        FailedLogs.Clear();

        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

            var execution = dbContext.JobExecutions
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == _executionId);

            if (execution is null)
            {
                HeaderTextBlock.Text = "Execution Details";
                SummaryTextBlock.Text = "Execution not found.";
                CountersTextBlock.Text = string.Empty;
                return;
            }

            HeaderTextBlock.Text = $"Execution Details - {jobName}";
            var statusText = ExecutionDisplayFormatter.FormatStatus(
                execution.Status,
                execution.WarningCount,
                execution.ErrorCount,
                execution.FilesFailed);

            SummaryTextBlock.Text =
                $"Status: {statusText}  |  Start: {ExecutionDisplayFormatter.FormatTimestamp(execution.StartTime)}  |  End: {ExecutionDisplayFormatter.FormatTimestamp(execution.EndTime)}  |  Duration: {ExecutionDisplayFormatter.FormatDuration(execution.Duration)}";

            CountersTextBlock.Text =
                $"Scanned: {execution.FilesScanned}  Copied: {execution.FilesCopied}  Updated: {execution.FilesUpdated}  Deleted: {execution.FilesDeleted}  Skipped: {execution.FilesSkipped}  Failed: {execution.FilesFailed}  Warnings: {execution.WarningCount}  Errors: {execution.ErrorCount}  Bytes: {execution.BytesTransferred:N0}";

            var logs = dbContext.ExecutionLogs
                .AsNoTracking()
                .Where(x => x.JobExecutionId == _executionId)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new ExecutionLogRow
                {
                    TimestampLocal = ExecutionDisplayFormatter.FormatTimestamp(x.Timestamp),
                    LevelValue = x.Level,
                    Level = x.Level.ToString(),
                    FilePath = x.FilePath ?? string.Empty,
                    Message = x.Message,
                    OperationType = x.OperationType
                })
                .ToList();

            foreach (var log in logs.Where(x => x.OperationType == OperationType.Copy))
            {
                CopyLogs.Add(log);
            }

            foreach (var log in logs.Where(x => x.OperationType == OperationType.Update))
            {
                UpdateLogs.Add(log);
            }

            foreach (var log in logs.Where(x => x.OperationType == OperationType.Delete))
            {
                DeleteLogs.Add(log);
            }

            foreach (var log in logs.Where(x => x.OperationType == OperationType.Skip))
            {
                SkippedLogs.Add(log);
            }

            foreach (var log in logs.Where(x => x.LevelValue == LogLevel.Error))
            {
                FailedLogs.Add(log);
            }

            CopyExpander.Header = $"Copy ({CopyLogs.Count})";
            UpdateExpander.Header = $"Update ({UpdateLogs.Count})";
            DeleteExpander.Header = $"Delete ({DeleteLogs.Count})";
            SkippedExpander.Header = $"Skipped ({SkippedLogs.Count})";
            FailedExpander.Header = $"Failed ({FailedLogs.Count})";
            ApplySectionVisibility();
        }
        catch (Exception ex)
        {
            HeaderTextBlock.Text = "Execution Details";
            SummaryTextBlock.Text = $"Unable to load execution details. {ex.Message}";
            CountersTextBlock.Text = string.Empty;
            ApplySectionVisibility();
        }
    }

    private void ApplySectionVisibility()
    {
        CopyExpander.Visibility = CopyLogs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateExpander.Visibility = UpdateLogs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DeleteExpander.Visibility = DeleteLogs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SkippedExpander.Visibility = SkippedLogs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        FailedExpander.Visibility = Visibility.Visible;
        NoFailedTextBlock.Visibility = FailedLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FailedLogsDataGrid.Visibility = FailedLogs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FailedExpander.IsExpanded = FailedLogs.Count > 0;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public sealed class ExecutionLogRow
    {
        public string TimestampLocal { get; init; } = string.Empty;

        public LogLevel LevelValue { get; init; }

        public string Level { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public OperationType? OperationType { get; init; }
    }
}
