using System.Windows;
using Archive.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Archive.Desktop;

public partial class SettingsWindow : Window
{
    private bool _isLoading;

    public SettingsWindow()
    {
        InitializeComponent();
        LogRetentionUnitComboBox.ItemsSource = Enum.GetValues<LogRetentionUnit>();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;

        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IArchiveApplicationSettingsService>();
            var settings = await settingsService.GetAsync();

            RunOnStartupCheckBox.IsChecked = WindowsStartupRegistrationService.IsEnabled() || settings.RunOnWindowsStartup;
            EnableNotificationsCheckBox.IsChecked = settings.EnableNotifications;
            NotifyOnStartCheckBox.IsChecked = settings.NotifyOnStart;
            NotifyOnCompleteCheckBox.IsChecked = settings.NotifyOnComplete;
            NotifyOnFailCheckBox.IsChecked = settings.NotifyOnFail;
            PlaySoundCheckBox.IsChecked = settings.PlayNotificationSound;
            LogRetentionValueTextBox.Text = settings.LogRetentionValue.ToString();
            LogRetentionUnitComboBox.SelectedItem = settings.LogRetentionUnit;
            EnableVerboseLoggingCheckBox.IsChecked = settings.EnableVerboseLogging;
            ApplyNotificationControlState(settings.EnableNotifications);
        }
        catch
        {
            RunOnStartupCheckBox.IsChecked = WindowsStartupRegistrationService.IsEnabled();
            EnableNotificationsCheckBox.IsChecked = true;
            NotifyOnStartCheckBox.IsChecked = false;
            NotifyOnCompleteCheckBox.IsChecked = true;
            NotifyOnFailCheckBox.IsChecked = true;
            PlaySoundCheckBox.IsChecked = true;
            LogRetentionValueTextBox.Text = "14";
            LogRetentionUnitComboBox.SelectedItem = LogRetentionUnit.Days;
            EnableVerboseLoggingCheckBox.IsChecked = false;
            ApplyNotificationControlState(true);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void SettingControl_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var enableNotifications = EnableNotificationsCheckBox.IsChecked ?? true;
        ApplyNotificationControlState(enableNotifications);

        if (!int.TryParse(LogRetentionValueTextBox.Text.Trim(), out var retentionValue) || retentionValue < 0)
        {
            return;
        }

        if (LogRetentionUnitComboBox.SelectedItem is not LogRetentionUnit retentionUnit)
        {
            return;
        }

        var runOnStartup = RunOnStartupCheckBox.IsChecked ?? false;
        if (!WindowsStartupRegistrationService.SetEnabled(runOnStartup))
        {
            System.Windows.MessageBox.Show(
                "Unable to update Windows startup registration.",
                "Archive",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IArchiveApplicationSettingsService>();
            await settingsService.SetAsync(new ArchiveApplicationSettings
            {
                RunOnWindowsStartup = runOnStartup,
                EnableNotifications = enableNotifications,
                NotifyOnStart = NotifyOnStartCheckBox.IsChecked ?? false,
                NotifyOnComplete = NotifyOnCompleteCheckBox.IsChecked ?? true,
                NotifyOnFail = NotifyOnFailCheckBox.IsChecked ?? true,
                PlayNotificationSound = PlaySoundCheckBox.IsChecked ?? true,
                LogRetentionValue = retentionValue,
                LogRetentionUnit = retentionUnit,
                EnableVerboseLogging = EnableVerboseLoggingCheckBox.IsChecked ?? false
            });
        }
        catch
        {
            System.Windows.MessageBox.Show(
                "Unable to save settings. Check logs for details.",
                "Archive",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplyNotificationControlState(bool enableNotifications)
    {
        NotifyOnStartCheckBox.IsEnabled = enableNotifications;
        NotifyOnCompleteCheckBox.IsEnabled = enableNotifications;
        NotifyOnFailCheckBox.IsEnabled = enableNotifications;
        PlaySoundCheckBox.IsEnabled = enableNotifications;
    }
}
