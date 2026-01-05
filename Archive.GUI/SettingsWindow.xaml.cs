using System.Windows;
using Archive.GUI.Services;

namespace Archive.GUI;

/// <summary>
/// Settings window for application configuration.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly DatabaseService _databaseService;

    public SettingsWindow(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        ChkRunOnStartup.IsChecked = StartupManager.IsSetToRunOnStartup();
    }

    private void ChkRunOnStartup_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = ChkRunOnStartup.IsChecked ?? false;
        var success = StartupManager.SetRunOnStartup(isChecked);

        if (!success)
        {
            MessageBox.Show(
                "Failed to update Windows startup settings. Please check your permissions.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Revert the check state
            ChkRunOnStartup.IsChecked = !isChecked;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
