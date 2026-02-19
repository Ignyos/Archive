using System.Windows;
using System.IO;
using Archive.Core.Domain.Enums;
using Archive.Core.Domain.Entities;
using Archive.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Forms = System.Windows.Forms;

namespace Archive.Desktop;

public partial class JobEditWindow : Window
{
    private readonly Guid _jobId;
    private readonly SyncMode _syncMode;
    private readonly ComparisonMethod _comparisonMethod;
    private readonly OverwriteBehavior _overwriteBehavior;
    private RecurringScheduleMode? _previousRecurringMode;
    private bool _suppressDeleteOrphanedPrompt;

    public JobEditWindow(JobListItemViewModel selectedJob)
    {
        InitializeComponent();

        _jobId = selectedJob.Id;
        _syncMode = selectedJob.SyncMode;
        _comparisonMethod = selectedJob.ComparisonMethod;
        _overwriteBehavior = selectedJob.OverwriteBehavior;
        NameTextBox.Text = selectedJob.Name;
        DescriptionTextBox.Text = selectedJob.Description ?? string.Empty;
        SourcePathTextBox.Text = selectedJob.SourcePath;
        DestinationPathTextBox.Text = selectedJob.DestinationPath;
        TriggerTypeComboBox.ItemsSource = Enum.GetValues<TriggerType>();
        TriggerTypeComboBox.SelectedItem = selectedJob.TriggerType;

        RecurringModeComboBox.ItemsSource = Enum.GetValues<RecurringScheduleMode>();
        SimpleFrequencyComboBox.ItemsSource = Enum.GetValues<SimpleRecurringFrequency>();
        SimpleDayOfWeekComboBox.ItemsSource = Enum.GetValues<DayOfWeek>();

        CronExpressionTextBox.Text = selectedJob.CronExpression ?? string.Empty;

        InitializeRecurringMode(selectedJob.CronExpression);

        var oneTimeLocal = selectedJob.SimpleTriggerTime?.ToLocalTime();
        OneTimeDatePicker.SelectedDate = oneTimeLocal?.Date;
        OneTimeTimeTextBox.Text = oneTimeLocal?.ToString("HH:mm") ?? DateTime.Now.AddHours(1).ToString("HH:mm");

        _suppressDeleteOrphanedPrompt = true;
        RecursiveCheckBox.IsChecked = selectedJob.Recursive;
        DeleteOrphanedCheckBox.IsChecked = selectedJob.DeleteOrphaned;
        SkipHiddenAndSystemCheckBox.IsChecked = selectedJob.SkipHiddenAndSystem;
        VerifyAfterCopyCheckBox.IsChecked = selectedJob.VerifyAfterCopy;
        _suppressDeleteOrphanedPrompt = false;

        EnabledCheckBox.IsChecked = selectedJob.Enabled;
        Title = $"Edit Job - {selectedJob.Name}";
        RefreshSchedulingUi();
    }

    private void ScheduleInput_OnChanged(object sender, RoutedEventArgs e)
    {
        SyncAdvancedCronOnModeTransition(sender);
        RefreshSchedulingUi();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ValidationTextBlock.Text = "Name is required.";
            return;
        }

        var sourcePath = SourcePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            ValidationTextBlock.Text = "Source path is required.";
            return;
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            ValidationTextBlock.Text = "Source path must exist and be accessible.";
            return;
        }

        var destinationPath = DestinationPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            ValidationTextBlock.Text = "Destination path is required.";
            return;
        }

        if (!Directory.Exists(destinationPath))
        {
            ValidationTextBlock.Text = "Destination path must be an existing accessible directory.";
            return;
        }

        if (TriggerTypeComboBox.SelectedItem is not TriggerType triggerType)
        {
            ValidationTextBlock.Text = "Trigger type is required.";
            return;
        }

        string? cronExpression = null;
        DateTime? simpleTriggerTime = null;

        switch (triggerType)
        {
            case TriggerType.Recurring:
                if (!TryBuildRecurringCron(out cronExpression, out var recurringError))
                {
                    ValidationTextBlock.Text = recurringError;
                    return;
                }
                break;

            case TriggerType.OneTime:
                if (!OneTimeDatePicker.SelectedDate.HasValue)
                {
                    ValidationTextBlock.Text = "One-time date is required.";
                    return;
                }

                if (!TimeSpan.TryParseExact(OneTimeTimeTextBox.Text.Trim(), "hh\\:mm", null, out var oneTimeClock))
                {
                    ValidationTextBlock.Text = "One-time time must be valid (HH:mm).";
                    return;
                }

                simpleTriggerTime = OneTimeDatePicker.SelectedDate.Value.Date.Add(oneTimeClock);
                if (simpleTriggerTime <= DateTime.Now)
                {
                    ValidationTextBlock.Text = "One-time schedule must be in the future.";
                    return;
                }
                break;

            case TriggerType.Manual:
                break;
        }

        if (string.Equals(
                sourcePath.TrimEnd('\\', '/'),
                destinationPath.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase))
        {
            ValidationTextBlock.Text = "Source and destination cannot be the same path.";
            return;
        }

        if (IsDestinationNestedWithinSource(sourcePath, destinationPath))
        {
            ValidationTextBlock.Text = "Destination cannot be inside the source path.";
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var backupJobStateService = scope.ServiceProvider.GetRequiredService<IBackupJobStateService>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<IJobSchedulerService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<Archive.Infrastructure.Persistence.ArchiveDbContext>();

            var isNameDuplicate = await dbContext.BackupJobs
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != _jobId
                    && x.DeletedAt == null
                    && x.Name != null
                    && x.Name.ToLower() == name.ToLower());

            if (isNameDuplicate)
            {
                ValidationTextBlock.Text = "Job name must be unique.";
                return;
            }

            var saved = await backupJobStateService.UpdateBasicFieldsAsync(
                _jobId,
                name,
                DescriptionTextBox.Text,
                sourcePath,
                destinationPath,
                EnabledCheckBox.IsChecked ?? false,
                triggerType,
                cronExpression,
                simpleTriggerTime,
                recursive: RecursiveCheckBox.IsChecked ?? true,
                deleteOrphaned: DeleteOrphanedCheckBox.IsChecked ?? false,
                skipHiddenAndSystem: SkipHiddenAndSystemCheckBox.IsChecked ?? true,
                verifyAfterCopy: VerifyAfterCopyCheckBox.IsChecked ?? false);

            if (!saved)
            {
                ValidationTextBlock.Text = "Unable to save. Check trigger and path values.";
                return;
            }

            await schedulerService.ScheduleJobAsync(_jobId);

            DialogResult = true;
            Close();
        }
        catch
        {
            ValidationTextBlock.Text = "Unable to save changes. Check logs for details.";
        }
    }

    private void PreviewOperationsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuildTransientJobFromForm(out var transientJob, out var validationError) || transientJob is null)
        {
            ValidationTextBlock.Text = validationError;
            return;
        }

        try
        {
            ValidationTextBlock.Text = "Generating preview...";
            Cursor = System.Windows.Input.Cursors.Wait;

            var preview = JobPreviewService.BuildPreview(transientJob);
            ValidationTextBlock.Text = string.Empty;

            var previewWindow = new JobPreviewWindow(preview)
            {
                Owner = this
            };

            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            ValidationTextBlock.Text = $"Unable to generate preview: {ex.Message}";
        }
        finally
        {
            Cursor = null;
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BrowseSourceFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = PickFolder(SourcePathTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SourcePathTextBox.Text = selected;
        }
    }

    private void BrowseSourceFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            CheckFileExists = true,
            CheckPathExists = true,
            Filter = "All files (*.*)|*.*"
        };

        if (TryGetExistingDirectory(SourcePathTextBox.Text, out var initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        var result = dialog.ShowDialog(this);
        if (result == true)
        {
            SourcePathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseDestinationFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = PickFolder(DestinationPathTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            DestinationPathTextBox.Text = selected;
        }
    }

    private void DeleteOrphanedCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressDeleteOrphanedPrompt)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            "Delete orphaned destination files is destructive and may permanently remove files not present in source. Enable this option?",
            "Confirm Delete Orphaned",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation == MessageBoxResult.Yes)
        {
            return;
        }

        _suppressDeleteOrphanedPrompt = true;
        DeleteOrphanedCheckBox.IsChecked = false;
        _suppressDeleteOrphanedPrompt = false;
    }

    private void RefreshSchedulingUi()
    {
        if (TriggerTypeComboBox.SelectedItem is not TriggerType triggerType)
        {
            CronRowGrid.Visibility = Visibility.Collapsed;
            RecurringModeRowGrid.Visibility = Visibility.Collapsed;
            SimpleRecurringRowGrid.Visibility = Visibility.Collapsed;
            OneTimeRowGrid.Visibility = Visibility.Collapsed;
            SimpleCronPreviewTextBlock.Text = string.Empty;
            SchedulePreviewTextBlock.Text = "Select a trigger type to preview schedule behavior.";
            return;
        }

        var recurringMode = GetRecurringMode();

        RecurringModeRowGrid.Visibility = triggerType == TriggerType.Recurring
            ? Visibility.Visible
            : Visibility.Collapsed;

        SimpleRecurringRowGrid.Visibility = triggerType == TriggerType.Recurring && recurringMode == RecurringScheduleMode.Simple
            ? Visibility.Visible
            : Visibility.Collapsed;

        CronRowGrid.Visibility = triggerType == TriggerType.Recurring && recurringMode == RecurringScheduleMode.Advanced
            ? Visibility.Visible
            : Visibility.Collapsed;

        OneTimeRowGrid.Visibility = triggerType == TriggerType.OneTime
            ? Visibility.Visible
            : Visibility.Collapsed;

        var oneTimeLocal = TryGetOneTimeLocalValue();
        var cronForPreview = triggerType == TriggerType.Recurring
            ? TryBuildRecurringCronForPreview()
            : CronExpressionTextBox.Text;

        if (triggerType == TriggerType.Recurring && recurringMode == RecurringScheduleMode.Simple)
        {
            SimpleCronPreviewTextBlock.Text = string.IsNullOrWhiteSpace(cronForPreview)
                ? "Generated cron: enter valid Simple schedule values."
                : $"Generated cron: {cronForPreview}";
        }
        else
        {
            SimpleCronPreviewTextBlock.Text = string.Empty;
        }

        SchedulePreviewTextBlock.Text = SchedulePreviewService.Build(
            triggerType,
            cronForPreview,
            oneTimeLocal,
            DateTime.Now);

        UpdateSimpleFrequencyVisibility();
    }

    private DateTime? TryGetOneTimeLocalValue()
    {
        if (!OneTimeDatePicker.SelectedDate.HasValue)
        {
            return null;
        }

        if (!TimeSpan.TryParseExact(OneTimeTimeTextBox.Text.Trim(), "hh\\:mm", null, out var clock))
        {
            return null;
        }

        return OneTimeDatePicker.SelectedDate.Value.Date.Add(clock);
    }

    private void InitializeRecurringMode(string? cronExpression)
    {
        SimpleTimeTextBox.Text = "02:00";
        SimpleDayOfMonthTextBox.Text = "1";
        SimpleFrequencyComboBox.SelectedItem = SimpleRecurringFrequency.Daily;
        SimpleDayOfWeekComboBox.SelectedItem = DayOfWeek.Monday;

        if (RecurringCronModeService.TryParseSimpleRecurring(cronExpression ?? string.Empty, out var simpleConfig) && simpleConfig is not null)
        {
            RecurringModeComboBox.SelectedItem = RecurringScheduleMode.Simple;
            SimpleFrequencyComboBox.SelectedItem = simpleConfig.Frequency;
            SimpleDayOfWeekComboBox.SelectedItem = simpleConfig.DayOfWeek;
            SimpleDayOfMonthTextBox.Text = simpleConfig.DayOfMonth.ToString();
            SimpleTimeTextBox.Text = simpleConfig.TimeOfDayText;
            _previousRecurringMode = GetRecurringMode();
            return;
        }

        RecurringModeComboBox.SelectedItem = RecurringScheduleMode.Advanced;
        _previousRecurringMode = GetRecurringMode();
    }

    private void SyncAdvancedCronOnModeTransition(object sender)
    {
        if (!ReferenceEquals(sender, RecurringModeComboBox))
        {
            return;
        }

        var currentMode = GetRecurringMode();

        if (_previousRecurringMode == RecurringScheduleMode.Simple
            && currentMode == RecurringScheduleMode.Advanced
            && TryBuildRecurringCronForSimpleMode(out var generatedCron)
            && !string.IsNullOrWhiteSpace(generatedCron))
        {
            CronExpressionTextBox.Text = generatedCron;
        }

        _previousRecurringMode = currentMode;
    }

    private RecurringScheduleMode GetRecurringMode()
    {
        return RecurringModeComboBox.SelectedItem is RecurringScheduleMode mode
            ? mode
            : RecurringScheduleMode.Advanced;
    }

    private bool TryBuildRecurringCron(out string? cronExpression, out string error)
    {
        cronExpression = null;
        error = "";

        if (GetRecurringMode() == RecurringScheduleMode.Advanced)
        {
            var cron = CronExpressionTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cron))
            {
                error = "Cron expression is required for recurring schedules.";
                return false;
            }

            cronExpression = cron;
            return true;
        }

        try
        {
            if (SimpleFrequencyComboBox.SelectedItem is not SimpleRecurringFrequency frequency)
            {
                error = "Simple schedule frequency is required.";
                return false;
            }

            cronExpression = frequency switch
            {
                SimpleRecurringFrequency.Daily => RecurringCronModeService.BuildDailyCron(SimpleTimeTextBox.Text),
                SimpleRecurringFrequency.Weekly when SimpleDayOfWeekComboBox.SelectedItem is DayOfWeek dayOfWeek
                    => RecurringCronModeService.BuildWeeklyCron(dayOfWeek, SimpleTimeTextBox.Text),
                SimpleRecurringFrequency.Monthly when int.TryParse(SimpleDayOfMonthTextBox.Text.Trim(), out var dayOfMonth)
                    => RecurringCronModeService.BuildMonthlyCron(dayOfMonth, SimpleTimeTextBox.Text),
                SimpleRecurringFrequency.Weekly => throw new FormatException("Select a weekday for weekly frequency."),
                _ => throw new FormatException("Enter a valid month day (1-31) for monthly frequency.")
            };

            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            error = ex.Message;
            return false;
        }
    }

    private string? TryBuildRecurringCronForPreview()
    {
        return TryBuildRecurringCron(out var cronExpression, out _)
            ? cronExpression
            : null;
    }

    private bool TryBuildRecurringCronForSimpleMode(out string? cronExpression)
    {
        cronExpression = null;

        var originalMode = GetRecurringMode();
        if (originalMode != RecurringScheduleMode.Simple)
        {
            return false;
        }

        return TryBuildRecurringCron(out cronExpression, out _);
    }

    private void UpdateSimpleFrequencyVisibility()
    {
        if (SimpleFrequencyComboBox.SelectedItem is not SimpleRecurringFrequency frequency)
        {
            SimpleDayOfWeekComboBox.IsEnabled = false;
            SimpleDayOfMonthTextBox.IsEnabled = false;
            return;
        }

        SimpleDayOfWeekComboBox.IsEnabled = frequency == SimpleRecurringFrequency.Weekly;
        SimpleDayOfMonthTextBox.IsEnabled = frequency == SimpleRecurringFrequency.Monthly;
    }

    private static string? PickFolder(string currentPath)
    {
        using var dialog = new Forms.FolderBrowserDialog();

        if (TryGetExistingDirectory(currentPath, out var initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        var result = dialog.ShowDialog();
        return result == Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    private static bool TryGetExistingDirectory(string pathText, out string directory)
    {
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(pathText))
        {
            return false;
        }

        var path = pathText.Trim();
        if (Directory.Exists(path))
        {
            directory = path;
            return true;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            return false;
        }

        directory = parent;
        return true;
    }

    private static bool IsDestinationNestedWithinSource(string sourcePath, string destinationPath)
    {
        var normalizedSource = NormalizePathForComparison(sourcePath);
        var normalizedDestination = NormalizePathForComparison(destinationPath);

        if (string.IsNullOrWhiteSpace(normalizedSource) || string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return false;
        }

        return normalizedDestination.StartsWith(normalizedSource + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim()).TrimEnd('\\', '/');
        }
        catch
        {
            return path.Trim().TrimEnd('\\', '/');
        }
    }

    private bool TryBuildTransientJobFromForm(out BackupJob? job, out string validationError)
    {
        job = null;
        validationError = string.Empty;

        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            validationError = "Name is required.";
            return false;
        }

        var sourcePath = SourcePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            validationError = "Source path is required.";
            return false;
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            validationError = "Source path must exist and be accessible.";
            return false;
        }

        var destinationPath = DestinationPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            validationError = "Destination path is required.";
            return false;
        }

        if (!Directory.Exists(destinationPath))
        {
            validationError = "Destination path must be an existing accessible directory.";
            return false;
        }

        if (string.Equals(
                sourcePath.TrimEnd('\\', '/'),
                destinationPath.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase))
        {
            validationError = "Source and destination cannot be the same path.";
            return false;
        }

        if (IsDestinationNestedWithinSource(sourcePath, destinationPath))
        {
            validationError = "Destination cannot be inside the source path.";
            return false;
        }

        if (TriggerTypeComboBox.SelectedItem is not TriggerType triggerType)
        {
            validationError = "Trigger type is required.";
            return false;
        }

        string? cronExpression = null;
        DateTime? simpleTriggerTime = null;

        switch (triggerType)
        {
            case TriggerType.Recurring:
                if (!TryBuildRecurringCron(out cronExpression, out var recurringError))
                {
                    validationError = recurringError;
                    return false;
                }
                break;

            case TriggerType.OneTime:
                if (!OneTimeDatePicker.SelectedDate.HasValue)
                {
                    validationError = "One-time date is required.";
                    return false;
                }

                if (!TimeSpan.TryParseExact(OneTimeTimeTextBox.Text.Trim(), "hh\\:mm", null, out var oneTimeClock))
                {
                    validationError = "One-time time must be valid (HH:mm).";
                    return false;
                }

                simpleTriggerTime = OneTimeDatePicker.SelectedDate.Value.Date.Add(oneTimeClock);
                if (simpleTriggerTime <= DateTime.Now)
                {
                    validationError = "One-time schedule must be in the future.";
                    return false;
                }
                break;
        }

        job = new BackupJob
        {
            Id = _jobId,
            Name = name,
            Description = DescriptionTextBox.Text,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Enabled = EnabledCheckBox.IsChecked ?? false,
            TriggerType = triggerType,
            CronExpression = cronExpression,
            SimpleTriggerTime = simpleTriggerTime,
            SyncMode = _syncMode,
            ComparisonMethod = _comparisonMethod,
            OverwriteBehavior = _overwriteBehavior,
            SyncOptions = new SyncOptions
            {
                Recursive = RecursiveCheckBox.IsChecked ?? true,
                DeleteOrphaned = DeleteOrphanedCheckBox.IsChecked ?? false,
                SkipHiddenAndSystem = SkipHiddenAndSystemCheckBox.IsChecked ?? true,
                VerifyAfterCopy = VerifyAfterCopyCheckBox.IsChecked ?? false
            }
        };

        return true;
    }
}