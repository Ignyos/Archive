using System.Windows;

namespace Archive.Desktop;

public partial class JobHistoryWindow : Window
{
    public JobHistoryWindow(JobListItemViewModel selectedJob)
    {
        InitializeComponent();
        LoadJob(selectedJob);
    }

    public void LoadJob(JobListItemViewModel selectedJob)
    {
        JobNameTextBlock.Text = selectedJob.Name;
        Title = $"Job History - {selectedJob.Name}";
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}