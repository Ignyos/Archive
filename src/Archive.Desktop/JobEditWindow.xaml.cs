using System.Windows;
using Archive.Core.Domain.Enums;
using Archive.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Archive.Desktop;

public partial class JobEditWindow : Window
{
    private readonly Guid _jobId;

    public JobEditWindow(JobListItemViewModel selectedJob)
    {
        InitializeComponent();

        _jobId = selectedJob.Id;
        NameTextBox.Text = selectedJob.Name;
        DescriptionTextBox.Text = selectedJob.Description ?? string.Empty;
        SourcePathTextBox.Text = selectedJob.SourcePath;
        DestinationPathTextBox.Text = selectedJob.DestinationPath;
        TriggerTypeComboBox.ItemsSource = Enum.GetValues<TriggerType>();
        TriggerTypeComboBox.SelectedItem = selectedJob.TriggerType;
        CronExpressionTextBox.Text = selectedJob.CronExpression ?? string.Empty;

        var oneTimeLocal = selectedJob.SimpleTriggerTime?.ToLocalTime();
        OneTimeDatePicker.SelectedDate = oneTimeLocal?.Date;
        OneTimeTimeTextBox.Text = oneTimeLocal?.ToString("HH:mm") ?? DateTime.Now.AddHours(1).ToString("HH:mm");

        EnabledCheckBox.IsChecked = selectedJob.Enabled;
        Title = $"Edit Job - {selectedJob.Name}";
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

        var destinationPath = DestinationPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            ValidationTextBlock.Text = "Destination path is required.";
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
                cronExpression = CronExpressionTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(cronExpression))
                {
                    ValidationTextBlock.Text = "Cron expression is required for recurring schedules.";
                    return;
                }
                break;

            case TriggerType.OneTime:
                if (!OneTimeDatePicker.SelectedDate.HasValue)
                {
                    ValidationTextBlock.Text = "One-time date is required.";
                    return;
                }

                if (!TimeSpan.TryParse(OneTimeTimeTextBox.Text.Trim(), out var oneTimeClock))
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

        try
        {
            using var scope = App.Services.CreateScope();
            var backupJobStateService = scope.ServiceProvider.GetRequiredService<IBackupJobStateService>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<IJobSchedulerService>();

            var saved = await backupJobStateService.UpdateBasicFieldsAsync(
                _jobId,
                name,
                DescriptionTextBox.Text,
                sourcePath,
                destinationPath,
                EnabledCheckBox.IsChecked ?? false,
                triggerType,
                cronExpression,
                simpleTriggerTime);

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

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}