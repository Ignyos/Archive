using System.Windows;
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

            var saved = await backupJobStateService.UpdateBasicFieldsAsync(
                _jobId,
                name,
                DescriptionTextBox.Text,
                sourcePath,
                destinationPath,
                EnabledCheckBox.IsChecked ?? false);

            if (!saved)
            {
                ValidationTextBlock.Text = "Unable to save because the job no longer exists.";
                return;
            }

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