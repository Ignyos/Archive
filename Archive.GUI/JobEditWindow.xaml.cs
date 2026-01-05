using System.Windows;
using Microsoft.Win32;
using Archive.Core;

namespace Archive.GUI;

public partial class JobEditWindow : Window
{
    private readonly BackupJob _job;

    public JobEditWindow(BackupJob job)
    {
        InitializeComponent();
        _job = job;
        DataContext = _job;
        
        Loaded += JobEditWindow_Loaded;
    }

    private void JobEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load schedule days into checkboxes
        ChkMonday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Monday);
        ChkTuesday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Tuesday);
        ChkWednesday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Wednesday);
        ChkThursday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Thursday);
        ChkFriday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Friday);
        ChkSaturday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Saturday);
        ChkSunday.IsChecked = _job.Schedule.AllowedDays.Contains(DayOfWeek.Sunday);

        // Load time window if it exists
        if (_job.Schedule.AllowedTimeWindows.Count > 0)
        {
            var timeWindow = _job.Schedule.AllowedTimeWindows[0];
            TxtStartTime.Value = DateTime.Today.Add(timeWindow.StartTime);
            
            // Only load cutoff time if HasCutoff is true
            if (timeWindow.HasCutoff)
            {
                TxtEndTime.Value = DateTime.Today.Add(timeWindow.EndTime);
            }
        }
    }

    private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Source Folder"
        };

        if (!string.IsNullOrEmpty(_job.SourcePath))
            dialog.InitialDirectory = _job.SourcePath;

        if (dialog.ShowDialog() == true)
        {
            TxtSourcePath.Text = dialog.FolderName;
        }
    }

    private void BtnBrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Destination Folder"
        };

        if (!string.IsNullOrEmpty(_job.DestinationPath))
            dialog.InitialDirectory = _job.DestinationPath;

        if (dialog.ShowDialog() == true)
        {
            TxtDestinationPath.Text = dialog.FolderName;
        }
    }

    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(TxtJobName.Text))
        {
            MessageBox.Show("Please enter a job name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtSourcePath.Text))
        {
            MessageBox.Show("Please select a source folder.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtDestinationPath.Text))
        {
            MessageBox.Show("Please select a destination folder.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Update job properties
        _job.Name = TxtJobName.Text;
        _job.SourcePath = TxtSourcePath.Text;
        _job.DestinationPath = TxtDestinationPath.Text;
        _job.Description = TxtDescription.Text;
        _job.Enabled = ChkEnabled.IsChecked == true;
        _job.Options.Recursive = ChkRecursive.IsChecked == true;
        _job.Options.DeleteOrphaned = ChkDeleteOrphaned.IsChecked == true;
        _job.Options.VerifyAfterCopy = ChkVerify.IsChecked == true;

        // Update schedule
        _job.Schedule.Enabled = ChkScheduleEnabled.IsChecked == true;
        _job.Schedule.AllowedDays.Clear();
        
        // Only validate and update schedule if it's enabled
        if (_job.Schedule.Enabled)
        {
            // Validate that at least one day is selected
            if (ChkMonday.IsChecked != true && ChkTuesday.IsChecked != true && 
                ChkWednesday.IsChecked != true && ChkThursday.IsChecked != true &&
                ChkFriday.IsChecked != true && ChkSaturday.IsChecked != true && 
                ChkSunday.IsChecked != true)
            {
                MessageBox.Show("Please select at least one day for the schedule.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate that start time is provided
            if (TxtStartTime.Value == null)
            {
                MessageBox.Show("Please specify a start time for the schedule.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ChkMonday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Monday);
            if (ChkTuesday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Tuesday);
            if (ChkWednesday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Wednesday);
            if (ChkThursday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Thursday);
            if (ChkFriday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Friday);
            if (ChkSaturday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Saturday);
            if (ChkSunday.IsChecked == true) _job.Schedule.AllowedDays.Add(DayOfWeek.Sunday);
        }

        // Update time window
        _job.Schedule.AllowedTimeWindows.Clear();
        if (TxtStartTime.Value.HasValue)
        {
            try
            {
                var startTime = TxtStartTime.Value.Value.TimeOfDay;
                TimeSpan endTime;
                bool hasCutoff = TxtEndTime.Value.HasValue;

                // If cutoff time is provided, use it; otherwise, set a dummy value
                if (hasCutoff && TxtEndTime.Value.HasValue)
                {
                    endTime = TxtEndTime.Value.Value.TimeOfDay;
                }
                else
                {
                    // Dummy value - won't be used when HasCutoff is false
                    endTime = new TimeSpan(23, 59, 59);
                }

                _job.Schedule.AllowedTimeWindows.Add(new TimeWindow
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    HasCutoff = hasCutoff
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing time values.\n\n{ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnClearCutoff_Click(object sender, RoutedEventArgs e)
    {
        TxtEndTime.Value = null;
    }

    private async void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(TxtSourcePath.Text))
        {
            MessageBox.Show("Please select a source folder first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtDestinationPath.Text))
        {
            MessageBox.Show("Please select a destination folder first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create a temporary job with current settings
        var previewJob = new BackupJob
        {
            Name = string.IsNullOrWhiteSpace(TxtJobName.Text) ? "Preview" : TxtJobName.Text,
            SourcePath = TxtSourcePath.Text,
            DestinationPath = TxtDestinationPath.Text,
            Options = new SyncOptions
            {
                Recursive = ChkRecursive.IsChecked == true,
                DeleteOrphaned = ChkDeleteOrphaned.IsChecked == true,
                VerifyAfterCopy = ChkVerify.IsChecked == true,
                DryRun = true // Enable dry run mode
            }
        };

        // Create and show the report window immediately (before calculation)
        var reportWindow = new JobReportWindow(previewJob)
        {
            Owner = this,
            Title = "Operations Preview - " + previewJob.Name
        };
        reportWindow.Show();

        try
        {
            // Run the dry run
            var syncEngine = new SyncEngine();
            var result = await syncEngine.SynchronizeAsync(previewJob.SourcePath, previewJob.DestinationPath, previewJob.Options);

            // Update the report window with results
            reportWindow.UpdateWithResult(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error previewing operations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            reportWindow.Close();
        }
    }
}
