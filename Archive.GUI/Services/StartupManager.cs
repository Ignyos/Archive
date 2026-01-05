using Microsoft.Win32;
using System.IO;
using System.Runtime.Versioning;

namespace Archive.GUI.Services;

/// <summary>
/// Manages Windows startup configuration for the application.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupManager
{
    private const string AppName = "ArchiveGUI";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Checks if the application is set to run on Windows startup.
    /// </summary>
    /// <returns>True if set to run on startup; otherwise, false.</returns>
    public static bool IsSetToRunOnStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            var value = key?.GetValue(AppName) as string;
            
            if (string.IsNullOrEmpty(value))
                return false;

            // Verify the path points to current executable location
            var currentPath = GetExecutablePath();
            return string.Equals(value, currentPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the application to run on Windows startup.
    /// </summary>
    /// <returns>True if successful; otherwise, false.</returns>
    public static bool SetRunOnStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null)
                return false;

            if (enable)
            {
                var executablePath = GetExecutablePath();
                key.SetValue(AppName, executablePath);
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the full path to the current executable.
    /// </summary>
    private static string GetExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
            return processPath;

        // Fallback for development/debugging
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArchiveGUI.exe");
    }
}
