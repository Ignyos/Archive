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
    private readonly bool _isCreateMode;
    private readonly SyncMode _syncMode;
    private readonly ComparisonMethod _comparisonMethod;
    private readonly OverwriteBehavior _overwriteBehavior;
    private bool _suppressDeleteOrphanedPrompt;
    private bool _suppressRecurringSync;

    public JobEditWindow()
    {
        InitializeComponent();

        _jobId = Guid.NewGuid();
        _isCreateMode = true;
        _syncMode = SyncMode.Incremental;
        _comparisonMethod = ComparisonMethod.Fast;
        _overwriteBehavior = OverwriteBehavior.AlwaysOverwrite;

        TriggerTypeComboBox.ItemsSource = Enum.GetValues<TriggerType>();
        SimpleFrequencyComboBox.ItemsSource = Enum.GetValues<SimpleRecurringFrequency>();
        SimpleDayOfWeekComboBox.ItemsSource = Enum.GetValues<DayOfWeek>();
        InitializeTimePickers();

        NameTextBox.Text = string.Empty;
        DescriptionTextBox.Text = string.Empty;
        SourcePathTextBox.Text = string.Empty;
        DestinationPathTextBox.Text = string.Empty;
        TriggerTypeComboBox.SelectedItem = TriggerType.Manual;
        InitializeRecurringControls(null);
        OneTimeDatePicker.SelectedDate = DateTime.Now.AddDays(1).Date;
        OneTimeTimeTextBox.Text = DateTime.Now.AddHours(1).ToString("HH:mm");

        _suppressDeleteOrphanedPrompt = true;
        RecursiveCheckBox.IsChecked = true;
        DeleteOrphanedCheckBox.IsChecked = false;
        SkipHiddenAndSystemCheckBox.IsChecked = true;
        VerifyAfterCopyCheckBox.IsChecked = false;
        _suppressDeleteOrphanedPrompt = false;

        EnabledCheckBox.IsChecked = true;
        Title = "New Job";
        HeaderTextBlock.Text = "New Job";
        RefreshSchedulingUi();
    }

    public JobEditWindow(JobListItemViewModel selectedJob)
    {
        InitializeComponent();

        _jobId = selectedJob.Id;
        _isCreateMode = false;
        _syncMode = selectedJob.SyncMode;
        _comparisonMethod = selectedJob.ComparisonMethod;
        _overwriteBehavior = selectedJob.OverwriteBehavior;
        NameTextBox.Text = selectedJob.Name;
        DescriptionTextBox.Text = selectedJob.Description ?? string.Empty;
        SourcePathTextBox.Text = selectedJob.SourcePath;
        DestinationPathTextBox.Text = selectedJob.DestinationPath;
        TriggerTypeComboBox.ItemsSource = Enum.GetValues<TriggerType>();
        TriggerTypeComboBox.SelectedItem = selectedJob.TriggerType;

        SimpleFrequencyComboBox.ItemsSource = Enum.GetValues<SimpleRecurringFrequency>();
        SimpleDayOfWeekComboBox.ItemsSource = Enum.GetValues<DayOfWeek>();
        InitializeTimePickers();

        CronExpressionTextBox.Text = selectedJob.CronExpression ?? string.Empty;
        InitializeRecurringControls(selectedJob.CronExpression);

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
        HeaderTextBlock.Text = "Edit Job";
        RefreshSchedulingUi();
    }

    private void ScheduleInput_OnChanged(object sender, RoutedEventArgs e)
    {
        SyncRecurringInputs(sender);
        RefreshSchedulingUi();
    }

    private void ScheduleInput_OnDropDownClosed(object? sender, EventArgs e)
    {
        SyncRecurringInputs(sender ?? this);
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

            var saved = _isCreateMode
                ? await CreateJobAsync(dbContext, name, sourcePath, destinationPath, triggerType, cronExpression, simpleTriggerTime)
                : await UpdateJobAsync(scope.ServiceProvider, name, sourcePath, destinationPath, triggerType, cronExpression, simpleTriggerTime);

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

    private async Task<bool> UpdateJobAsync(
        IServiceProvider serviceProvider,
        string name,
        string sourcePath,
        string destinationPath,
        TriggerType triggerType,
        string? cronExpression,
        DateTime? simpleTriggerTime)
    {
        var backupJobStateService = serviceProvider.GetRequiredService<IBackupJobStateService>();
        return await backupJobStateService.UpdateBasicFieldsAsync(
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
    }

    private async Task<bool> CreateJobAsync(
        Archive.Infrastructure.Persistence.ArchiveDbContext dbContext,
        string name,
        string sourcePath,
        string destinationPath,
        TriggerType triggerType,
        string? cronExpression,
        DateTime? simpleTriggerTime)
    {
        var now = DateTime.UtcNow;

        var normalizedOneTimeUtc = simpleTriggerTime.HasValue
            ? (simpleTriggerTime.Value.Kind == DateTimeKind.Utc
                ? simpleTriggerTime.Value
                : simpleTriggerTime.Value.ToUniversalTime())
            : (DateTime?)null;

        var syncOptions = new SyncOptions
        {
            Id = Guid.NewGuid(),
            Recursive = RecursiveCheckBox.IsChecked ?? true,
            DeleteOrphaned = DeleteOrphanedCheckBox.IsChecked ?? false,
            SkipHiddenAndSystem = SkipHiddenAndSystemCheckBox.IsChecked ?? true,
            VerifyAfterCopy = VerifyAfterCopyCheckBox.IsChecked ?? false
        };

        var job = new BackupJob
        {
            Id = _jobId,
            Name = name,
            Description = DescriptionTextBox.Text,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Enabled = EnabledCheckBox.IsChecked ?? false,
            TriggerType = triggerType,
            CronExpression = triggerType == TriggerType.Recurring ? cronExpression : null,
            SimpleTriggerTime = triggerType == TriggerType.OneTime ? normalizedOneTimeUtc : null,
            SyncMode = _syncMode,
            ComparisonMethod = _comparisonMethod,
            OverwriteBehavior = _overwriteBehavior,
            SyncOptions = syncOptions,
            SyncOptionsId = syncOptions.Id,
            CreatedAt = now,
            ModifiedAt = now
        };

        dbContext.SyncOptions.Add(syncOptions);
        dbContext.BackupJobs.Add(job);
        await dbContext.SaveChangesAsync();
        return true;
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
            SimpleRecurringRowGrid.Visibility = Visibility.Collapsed;
            OneTimeRowGrid.Visibility = Visibility.Collapsed;
            SchedulePreviewTextBlock.Text = "Select a trigger type to preview schedule behavior.";
            return;
        }

        SimpleRecurringRowGrid.Visibility = triggerType == TriggerType.Recurring
            ? Visibility.Visible
            : Visibility.Collapsed;

        CronRowGrid.Visibility = triggerType == TriggerType.Recurring
            ? Visibility.Visible
            : Visibility.Collapsed;

        OneTimeRowGrid.Visibility = triggerType == TriggerType.OneTime
            ? Visibility.Visible
            : Visibility.Collapsed;

        var oneTimeLocal = TryGetOneTimeLocalValue();
        var cronForPreview = triggerType == TriggerType.Recurring
            ? TryBuildRecurringCronForPreview()
            : CronExpressionTextBox.Text;

        SchedulePreviewTextBlock.Text = SchedulePreviewService.Build(
            triggerType,
            cronForPreview,
            oneTimeLocal,
            DateTime.Now);

        UpdateSimpleFrequencyVisibility();
    }

    private void InitializeTimePickers()
    {
        var timeOptions = BuildTimeOptions();
        SimpleTimeTextBox.ItemsSource = timeOptions;
        OneTimeTimeTextBox.ItemsSource = timeOptions;
    }

    private static List<string> BuildTimeOptions()
    {
        var options = new List<string>(96);
        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute += 15)
            {
                options.Add($"{hour:00}:{minute:00}");
            }
        }

        return options;
    }

    private DateTime? TryGetOneTimeLocalValue()
    {
        if (!OneTimeDatePicker.SelectedDate.HasValue)
        {
            return null;
        }

        var oneTimeText = GetComboBoxText(OneTimeTimeTextBox);
        if (!TimeSpan.TryParseExact(oneTimeText, "hh\\:mm", null, out var clock))
        {
            return null;
        }

        return OneTimeDatePicker.SelectedDate.Value.Date.Add(clock);
    }

    private void InitializeRecurringControls(string? cronExpression)
    {
        _suppressRecurringSync = true;
        SimpleTimeTextBox.Text = "00:00";
        SimpleDayOfMonthTextBox.Text = "1";
        SimpleFrequencyComboBox.SelectedItem = SimpleRecurringFrequency.Weekly;
        SimpleDayOfWeekComboBox.SelectedItem = DayOfWeek.Sunday;

        if (RecurringCronModeService.TryParseSimpleRecurring(cronExpression ?? string.Empty, out var simpleConfig) && simpleConfig is not null)
        {
            SimpleFrequencyComboBox.SelectedItem = simpleConfig.Frequency;
            SimpleDayOfWeekComboBox.SelectedItem = simpleConfig.DayOfWeek;
            SimpleDayOfMonthTextBox.Text = simpleConfig.DayOfMonth.ToString();
            SimpleTimeTextBox.Text = simpleConfig.TimeOfDayText;
        }
        else if (string.IsNullOrWhiteSpace(cronExpression)
            && TryBuildRecurringCronFromSimpleControls(out var generatedCron, out _)
            && !string.IsNullOrWhiteSpace(generatedCron))
        {
            CronExpressionTextBox.Text = generatedCron;
        }

        _suppressRecurringSync = false;
    }

    private void SyncRecurringInputs(object sender)
    {
        if (_suppressRecurringSync)
        {
            return;
        }

        if (TriggerTypeComboBox.SelectedItem is not TriggerType.Recurring)
        {
            return;
        }

        if (ReferenceEquals(sender, CronExpressionTextBox))
        {
            SyncSimpleControlsFromCronText();
            return;
        }

        if (IsSimpleRecurringControl(sender))
        {
            SyncCronTextFromSimpleControls();
        }
    }

    private static bool IsSimpleRecurringControl(object sender)
    {
        return sender is System.Windows.Controls.ComboBox or System.Windows.Controls.TextBox;
    }

    private void SyncSimpleControlsFromCronText()
    {
        var cron = CronExpressionTextBox.Text.Trim();
        if (!RecurringCronModeService.TryParseSimpleRecurring(cron, out var config) || config is null)
        {
            return;
        }

        _suppressRecurringSync = true;
        SimpleFrequencyComboBox.SelectedItem = config.Frequency;
        SimpleDayOfWeekComboBox.SelectedItem = config.DayOfWeek;
        SimpleDayOfMonthTextBox.Text = config.DayOfMonth.ToString();
        SimpleTimeTextBox.Text = config.TimeOfDayText;
        _suppressRecurringSync = false;
    }

    private void SyncCronTextFromSimpleControls()
    {
        if (!TryBuildRecurringCronFromSimpleControls(out var cronExpression, out _)
            || string.IsNullOrWhiteSpace(cronExpression)
            || string.Equals(CronExpressionTextBox.Text.Trim(), cronExpression, StringComparison.Ordinal))
        {
            return;
        }

        _suppressRecurringSync = true;
        CronExpressionTextBox.Text = cronExpression;
        _suppressRecurringSync = false;
    }

    private bool TryBuildRecurringCron(out string? cronExpression, out string error)
    {
        cronExpression = null;
        error = "";

        var cron = CronExpressionTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(cron))
        {
            cronExpression = cron;
            return true;
        }

        if (TryBuildRecurringCronFromSimpleControls(out var generatedCron, out error)
            && !string.IsNullOrWhiteSpace(generatedCron))
        {
            cronExpression = generatedCron;
            return true;
        }

        error = "Cron expression is required for recurring schedules.";
        return false;
    }

    private bool TryBuildRecurringCronFromSimpleControls(out string? cronExpression, out string error)
    {
        cronExpression = null;
        error = "";

        try
        {
            if (SimpleFrequencyComboBox.SelectedItem is not SimpleRecurringFrequency frequency)
            {
                error = "Recurring frequency is required.";
                return false;
            }

            cronExpression = frequency switch
            {
                SimpleRecurringFrequency.Daily => RecurringCronModeService.BuildDailyCron(GetComboBoxText(SimpleTimeTextBox)),
                SimpleRecurringFrequency.Weekly when SimpleDayOfWeekComboBox.SelectedItem is DayOfWeek dayOfWeek
                    => RecurringCronModeService.BuildWeeklyCron(dayOfWeek, GetComboBoxText(SimpleTimeTextBox)),
                SimpleRecurringFrequency.Monthly when int.TryParse(SimpleDayOfMonthTextBox.Text.Trim(), out var dayOfMonth)
                    => RecurringCronModeService.BuildMonthlyCron(dayOfMonth, GetComboBoxText(SimpleTimeTextBox)),
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

    private static string GetComboBoxText(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedItem is string selected)
        {
            return selected.Trim();
        }

        return comboBox.Text.Trim();
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