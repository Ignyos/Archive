using Microsoft.Win32;

namespace Archive.Desktop;

public static class WindowsStartupRegistrationService
{
    private const string RunRegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string StartupValueName = "Archive";

    public static bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
        var raw = runKey?.GetValue(StartupValueName) as string;
        return !string.IsNullOrWhiteSpace(raw);
    }

    public static bool SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
        if (runKey is null)
        {
            return false;
        }

        if (!enabled)
        {
            runKey.DeleteValue(StartupValueName, throwOnMissingValue: false);
            return true;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        runKey.SetValue(StartupValueName, $"\"{executablePath}\"");
        return true;
    }
}