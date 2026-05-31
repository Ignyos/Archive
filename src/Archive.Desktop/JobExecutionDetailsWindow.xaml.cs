using System.Collections.ObjectModel;
using System.Windows;
using Archive.Core.Domain.Enums;
using Archive.Core.Domain.Entities;
using Archive.Core.Sync;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Archive.Desktop;

public partial class JobExecutionDetailsWindow : Window
{
    private Guid _executionId;
    private Guid _jobId;
    private string _jobSourcePath = string.Empty;
    private string _autoSuggestedFailedPath = string.Empty;
    private string _autoSuggestedFailedMessage = string.Empty;

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
                .Include(x => x.Job)
                .FirstOrDefault(x => x.Id == _executionId);

            if (execution is null)
            {
                HeaderTextBlock.Text = "Execution Details";
                SummaryTextBlock.Text = "Execution not found.";
                CountersTextBlock.Text = string.Empty;
                return;
            }

            HeaderTextBlock.Text = $"Execution Details - {jobName}";
            _jobId = execution.JobId;
            _jobSourcePath = execution.Job?.SourcePath ?? string.Empty;
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
            EvaluateAutoSuggestion(dbContext);
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
        AutoSuggestBorder.Visibility = string.IsNullOrWhiteSpace(_autoSuggestedFailedPath)
            ? Visibility.Collapsed
            : Visibility.Visible;
        NoFailedTextBlock.Visibility = FailedLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FailedLogsDataGrid.Visibility = FailedLogs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FailedExpander.IsExpanded = FailedLogs.Count > 0;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void SuggestIgnoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ExecutionLogRow row })
        {
            return;
        }

        if (_jobId == Guid.Empty)
        {
            System.Windows.MessageBox.Show(
                "Unable to identify the job for this execution.",
                "Suggest Ignore Rule",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var picker = new SuggestIgnoreRuleWindow(
            _jobSourcePath,
            row.FilePath,
            row.Message,
            FailedLogs
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                .Select(x => x.FilePath)
                .ToList())
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedSuggestion is null)
        {
            return;
        }

        await AddSuggestionToJobAsync(picker.SelectedSuggestion, row.FilePath);
    }

    private async void AutoSuggestAddButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_autoSuggestedFailedPath))
        {
            return;
        }

        var picker = new SuggestIgnoreRuleWindow(
            _jobSourcePath,
            _autoSuggestedFailedPath,
            _autoSuggestedFailedMessage,
            FailedLogs
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                .Select(x => x.FilePath)
                .ToList())
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedSuggestion is null)
        {
            return;
        }

        await AddSuggestionToJobAsync(picker.SelectedSuggestion, _autoSuggestedFailedPath);
    }

    private async Task AddSuggestionToJobAsync(IgnoreRuleSuggestionService.Suggestion suggestion, string failedPath)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var added = await AddIgnoreRuleToJobAsync(dbContext, _jobId, suggestion.Rule);

            if (!added)
            {
                System.Windows.MessageBox.Show(
                    "This ignore rule already exists for the job.",
                    "Suggest Ignore Rule",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            System.Windows.MessageBox.Show(
                "Ignore rule added to the job. Future runs will skip matching paths.",
                "Suggest Ignore Rule",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _autoSuggestedFailedPath = string.Empty;
            _autoSuggestedFailedMessage = string.Empty;
            ApplySectionVisibility();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Unable to add ignore rule. {ex.Message}",
                "Suggest Ignore Rule",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void EvaluateAutoSuggestion(ArchiveDbContext dbContext)
    {
        _autoSuggestedFailedPath = string.Empty;
        _autoSuggestedFailedMessage = string.Empty;
        AutoSuggestTextBlock.Text = string.Empty;

        if (_jobId == Guid.Empty || FailedLogs.Count == 0)
        {
            ApplySectionVisibility();
            return;
        }

        var repeatedDeniedPaths = dbContext.ExecutionLogs
            .AsNoTracking()
            .Where(x => x.Level == LogLevel.Error
                && x.FilePath != null
                && x.Message.ToLower().Contains("denied"))
            .Join(
                dbContext.JobExecutions.AsNoTracking().Where(x => x.JobId == _jobId),
                log => log.JobExecutionId,
                execution => execution.Id,
                (log, _) => log.FilePath!)
            .GroupBy(x => x)
            .Where(x => x.Count() >= 2)
            .Select(x => x.Key)
            .ToList();

        if (repeatedDeniedPaths.Count == 0)
        {
            ApplySectionVisibility();
            return;
        }

        var repeatedSet = new HashSet<string>(repeatedDeniedPaths, StringComparer.OrdinalIgnoreCase);
        var failedRow = FailedLogs.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.FilePath)
            && x.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
            && repeatedSet.Contains(x.FilePath));

        if (failedRow is null)
        {
            ApplySectionVisibility();
            return;
        }

        var suggestions = IgnoreRuleSuggestionService
            .BuildSuggestions(
                _jobSourcePath,
                failedRow.FilePath,
                failedRow.Message,
                FailedLogs
                    .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                    .Select(x => x.FilePath)
                    .ToList());

        if (suggestions.Count == 0)
        {
            ApplySectionVisibility();
            return;
        }

        _autoSuggestedFailedPath = failedRow.FilePath;
        _autoSuggestedFailedMessage = failedRow.Message;
        AutoSuggestTextBlock.Text =
            $"Archive noticed repeated access-denied failures for '{failedRow.FilePath}'. Review {suggestions.Count} rule options before adding one.";
        AutoSuggestAddButton.Content = "Review Suggestions";
        ApplySectionVisibility();
    }

    private static async Task<bool> AddIgnoreRuleToJobAsync(ArchiveDbContext dbContext, Guid jobId, string rule)
    {
        var normalizedRule = IgnoreRuleMatcher.NormalizeRules([rule]).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(normalizedRule))
        {
            return false;
        }

        var exists = await dbContext.BackupJobExclusionPatterns
            .AsNoTracking()
            .Where(x => x.BackupJobId == jobId)
            .Include(x => x.ExclusionPattern)
            .AnyAsync(x => x.ExclusionPattern != null && x.ExclusionPattern.Pattern.ToLower() == normalizedRule.ToLower());

        if (exists)
        {
            return false;
        }

        var exclusionPattern = new ExclusionPattern
        {
            Id = Guid.NewGuid(),
            Name = "Job Ignore Rule",
            Pattern = normalizedRule,
            IsGlobal = false,
            IsSystemSuggestion = false
        };

        dbContext.ExclusionPatterns.Add(exclusionPattern);
        dbContext.BackupJobExclusionPatterns.Add(new BackupJobExclusionPattern
        {
            BackupJobId = jobId,
            ExclusionPatternId = exclusionPattern.Id,
            ExclusionPattern = exclusionPattern
        });

        await dbContext.SaveChangesAsync();
        return true;
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
