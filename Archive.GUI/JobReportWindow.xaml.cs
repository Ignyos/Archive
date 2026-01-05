using System.Windows;
using System.Windows.Media;
using Archive.Core;

namespace Archive.GUI;

/// <summary>
/// Window for displaying detailed job execution report.
/// </summary>
public partial class JobReportWindow : Window
{
    private readonly BackupJob _job;

    public JobReportWindow(BackupJob job, SyncResult result)
    {
        InitializeComponent();
        _job = job;
        LoadReport(job, result);
    }

    /// <summary>
    /// Constructor for showing window before operation starts.
    /// </summary>
    public JobReportWindow(BackupJob job)
    {
        InitializeComponent();
        _job = job;
        ShowCalculating(job);
    }

    /// <summary>
    /// Updates the window with the operation results.
    /// </summary>
    public void UpdateWithResult(SyncResult result)
    {
        LoadReport(_job, result);
    }

    private void ShowCalculating(BackupJob job)
    {
        TxtJobName.Text = $"{job.Name} - Calculating Operations...";

        // Hide summary until results are available
        SummaryGroupBox.Visibility = Visibility.Collapsed;

        // Show progress indicator in details
        ProgressPanel.Visibility = Visibility.Visible;
        TxtDetails.Visibility = Visibility.Collapsed;
    }

    private void LoadReport(BackupJob job, SyncResult result)
    {
        // Show summary and hide progress
        SummaryGroupBox.Visibility = Visibility.Visible;
        ProgressPanel.Visibility = Visibility.Collapsed;
        TxtDetails.Visibility = Visibility.Visible;

        TxtJobName.Text = $"{job.Name} - Report";

        // Status
        if (result.Success)
        {
            TxtStatus.Text = "Success";
            TxtStatus.Foreground = Brushes.Green;
        }
        else
        {
            TxtStatus.Text = "Failed";
            TxtStatus.Foreground = Brushes.Red;
        }

        // Duration
        TxtDuration.Text = result.Duration.ToString(@"hh\:mm\:ss");

        // Statistics
        TxtFilesCopied.Text = result.FilesCopied.ToString("N0");
        TxtFilesUpdated.Text = result.FilesUpdated.ToString("N0");
        TxtFilesDeleted.Text = result.FilesDeleted.ToString("N0");
        TxtFilesSkipped.Text = result.FilesSkipped.ToString("N0");
        TxtBytesCopied.Text = FormatBytes(result.BytesTransferred);
        TxtErrorCount.Text = result.Errors.Count.ToString();
        if (result.Errors.Count > 0)
        {
            TxtErrorCount.Foreground = Brushes.Red;
        }

        // Details
        var details = new System.Text.StringBuilder();
        details.AppendLine($"Source: {job.SourcePath}");
        details.AppendLine($"Destination: {job.DestinationPath}");
        details.AppendLine($"Completed: {job.LastRunTime:g}");
        details.AppendLine($"Duration: {result.Duration:hh\\:mm\\:ss}");
        details.AppendLine();

        if (result.Errors.Count > 0)
        {
            details.AppendLine("Errors:");
            foreach (var error in result.Errors)
            {
                details.AppendLine($"  • {error}");
            }
            details.AppendLine();
        }

        if (result.PlannedOperations.Count > 0)
        {
            details.AppendLine($"Operations ({result.PlannedOperations.Count}):");
            var grouped = result.PlannedOperations.GroupBy(o => o.Type);
            foreach (var group in grouped)
            {
                details.AppendLine($"  {group.Key}:");
                foreach (var op in group.Take(50)) // Limit to first 50 per type
                {
                    var path = op.DestinationPath ?? op.SourcePath ?? "Unknown";
                    details.AppendLine($"    • {path}");
                }
                if (group.Count() > 50)
                {
                    details.AppendLine($"    ... and {group.Count() - 50} more");
                }
            }
        }
        else
        {
            details.AppendLine("No operations were performed (all files up to date).");
        }

        TxtDetails.Text = details.ToString();
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

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
