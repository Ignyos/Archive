using System.IO;
using System.Text.Json;
using Archive.Core;

namespace Archive.GUI.Services;

/// <summary>
/// Handles persistence of backup jobs to local storage.
/// </summary>
public class JobStorageService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Archive");

    private static readonly string JobsFilePath = Path.Combine(AppDataPath, "jobs.json");
    private static readonly string BackupFilePath = Path.Combine(AppDataPath, "jobs.backup.json");

    public JobStorageService()
    {
        // Ensure AppData directory exists
        Directory.CreateDirectory(AppDataPath);
    }

    /// <summary>
    /// Loads the job collection from disk.
    /// </summary>
    public async Task<BackupJobCollection> LoadJobsAsync()
    {
        try
        {
            if (!File.Exists(JobsFilePath))
            {
                return CreateDefaultCollection();
            }

            var json = await File.ReadAllTextAsync(JobsFilePath);
            var collection = JsonSerializer.Deserialize<BackupJobCollection>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return collection ?? CreateDefaultCollection();
        }
        catch
        {
            // If load fails, try backup file
            if (File.Exists(BackupFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(BackupFilePath);
                    var collection = JsonSerializer.Deserialize<BackupJobCollection>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (collection != null)
                        return collection;
                }
                catch
                {
                    // Backup also failed, return default
                }
            }

            return CreateDefaultCollection();
        }
    }

    /// <summary>
    /// Saves the job collection to disk.
    /// </summary>
    public async Task SaveJobsAsync(BackupJobCollection collection)
    {
        try
        {
            // Backup existing file before overwriting
            if (File.Exists(JobsFilePath))
            {
                File.Copy(JobsFilePath, BackupFilePath, overwrite: true);
            }

            var json = JsonSerializer.Serialize(collection, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(JobsFilePath, json);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save jobs: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the path to the jobs file.
    /// </summary>
    public string GetJobsFilePath() => JobsFilePath;

    /// <summary>
    /// Gets the path to the AppData directory.
    /// </summary>
    public string GetAppDataPath() => AppDataPath;

    private BackupJobCollection CreateDefaultCollection()
    {
        return new BackupJobCollection
        {
            Name = "My Backup Jobs",
            Schedule = new OperationSchedule
            {
                Enabled = false
            }
        };
    }
}
