using System.IO;
using Microsoft.Data.Sqlite;
using Archive.Core;
using System.Text.Json;

namespace Archive.GUI.Services;

/// <summary>
/// Manages SQLite database for job storage, history, and logs.
/// </summary>
public class DatabaseService
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Archive");

        Directory.CreateDirectory(appDataPath);
        
        _databasePath = Path.Combine(appDataPath, "archive.db");
        _connectionString = $"Data Source={_databasePath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Jobs (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                SourcePath TEXT NOT NULL,
                DestinationPath TEXT NOT NULL,
                Enabled INTEGER NOT NULL,
                ShowNotifications INTEGER NOT NULL,
                Recursive INTEGER NOT NULL,
                DeleteOrphaned INTEGER NOT NULL,
                VerifyAfterCopy INTEGER NOT NULL,
                ScheduleEnabled INTEGER NOT NULL,
                AllowedDays TEXT,
                TimeWindowStart TEXT,
                TimeWindowEnd TEXT,
                CreatedAt TEXT NOT NULL,
                ModifiedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS JobHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                JobId TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                Success INTEGER NOT NULL,
                FilesCopied INTEGER NOT NULL,
                FilesUpdated INTEGER NOT NULL,
                FilesDeleted INTEGER NOT NULL,
                FilesSkipped INTEGER NOT NULL,
                BytesTransferred INTEGER NOT NULL,
                Duration TEXT NOT NULL,
                ErrorCount INTEGER NOT NULL,
                StoppedReason TEXT NOT NULL,
                FOREIGN KEY (JobId) REFERENCES Jobs(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS JobLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                JobHistoryId INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL,
                FOREIGN KEY (JobHistoryId) REFERENCES JobHistory(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_job_history_jobid ON JobHistory(JobId);
            CREATE INDEX IF NOT EXISTS idx_job_history_starttime ON JobHistory(StartTime);
            CREATE INDEX IF NOT EXISTS idx_job_logs_historyid ON JobLogs(JobHistoryId);
        ";
        command.ExecuteNonQuery();
    }

    public async Task<BackupJobCollection> LoadJobsAsync()
    {
        var collection = new BackupJobCollection();
        
        // Load settings
        using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Key, Value FROM Settings";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);
                
                switch (key)
                {
                    case "HasPromptedForStartup":
                        collection.HasPromptedForStartup = value == "1";
                        break;
                    case "HasLaunchedBefore":
                        collection.HasLaunchedBefore = value == "1";
                        break;
                    case "DisableAllNotifications":
                        collection.DisableAllNotifications = value == "1";
                        break;
                }
            }
        }

        // Load jobs
        using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Description, SourcePath, DestinationPath, Enabled, ShowNotifications,
                       Recursive, DeleteOrphaned, VerifyAfterCopy, ScheduleEnabled, AllowedDays,
                       TimeWindowStart, TimeWindowEnd
                FROM Jobs
                ORDER BY Name";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var job = new BackupJob
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    SourcePath = reader.GetString(3),
                    DestinationPath = reader.GetString(4),
                    Enabled = reader.GetInt32(5) == 1,
                    ShowNotifications = reader.GetInt32(6) == 1,
                    Options = new SyncOptions
                    {
                        Recursive = reader.GetInt32(7) == 1,
                        DeleteOrphaned = reader.GetInt32(8) == 1,
                        VerifyAfterCopy = reader.GetInt32(9) == 1
                    },
                    Schedule = new OperationSchedule
                    {
                        Enabled = reader.GetInt32(10) == 1
                    }
                };

                // Parse allowed days
                if (!reader.IsDBNull(11))
                {
                    var daysJson = reader.GetString(11);
                    var days = JsonSerializer.Deserialize<List<DayOfWeek>>(daysJson);
                    if (days != null)
                    {
                        job.Schedule.AllowedDays = days;
                    }
                }

                // Parse time window
                if (!reader.IsDBNull(12) && !reader.IsDBNull(13))
                {
                    var startTime = TimeSpan.Parse(reader.GetString(12));
                    var endTime = TimeSpan.Parse(reader.GetString(13));
                    job.Schedule.AllowedTimeWindows.Add(new TimeWindow
                    {
                        StartTime = startTime,
                        EndTime = endTime
                    });
                }

                // Load last result from history
                var historyCommand = connection.CreateCommand();
                historyCommand.CommandText = @"
                    SELECT StartTime, Success, FilesCopied, FilesUpdated, FilesDeleted, FilesSkipped,
                           BytesTransferred, Duration, ErrorCount, StoppedReason
                    FROM JobHistory
                    WHERE JobId = $jobId
                    ORDER BY StartTime DESC
                    LIMIT 1";
                historyCommand.Parameters.AddWithValue("$jobId", job.Id);
                
                using var historyReader = await historyCommand.ExecuteReaderAsync();
                if (await historyReader.ReadAsync())
                {
                    job.LastRunTime = DateTime.Parse(historyReader.GetString(0));
                    job.LastResult = new SyncResult
                    {
                        Success = historyReader.GetInt32(1) == 1,
                        FilesCopied = historyReader.GetInt32(2),
                        FilesUpdated = historyReader.GetInt32(3),
                        FilesDeleted = historyReader.GetInt32(4),
                        FilesSkipped = historyReader.GetInt32(5),
                        BytesTransferred = historyReader.GetInt64(6),
                        Duration = TimeSpan.Parse(historyReader.GetString(7)),
                        StoppedReason = Enum.Parse<SyncStoppedReason>(historyReader.GetString(9))
                    };
                    
                    // Set error count
                    for (int i = 0; i < historyReader.GetInt32(8); i++)
                    {
                        job.LastResult.Errors.Add("Error details in logs");
                    }
                }

                collection.Jobs.Add(job);
            }
        }

        return collection;
    }

    public async Task SaveJobsAsync(BackupJobCollection collection)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Save settings
            await SaveSettingAsync(connection, "HasPromptedForStartup", collection.HasPromptedForStartup ? "1" : "0");
            await SaveSettingAsync(connection, "HasLaunchedBefore", collection.HasLaunchedBefore ? "1" : "0");
            await SaveSettingAsync(connection, "DisableAllNotifications", collection.DisableAllNotifications ? "1" : "0");

            // Get existing job IDs
            var existingIds = new HashSet<string>();
            var getIdsCommand = connection.CreateCommand();
            getIdsCommand.CommandText = "SELECT Id FROM Jobs";
            using (var reader = await getIdsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingIds.Add(reader.GetString(0));
                }
            }

            // Save or update jobs
            foreach (var job in collection.Jobs)
            {
                var command = connection.CreateCommand();
                var now = DateTime.Now.ToString("o");
                
                var timeWindowStart = job.Schedule.AllowedTimeWindows.Count > 0 
                    ? job.Schedule.AllowedTimeWindows[0].StartTime.ToString() 
                    : null;
                var timeWindowEnd = job.Schedule.AllowedTimeWindows.Count > 0 
                    ? job.Schedule.AllowedTimeWindows[0].EndTime.ToString() 
                    : null;

                if (existingIds.Contains(job.Id))
                {
                    // Update existing job
                    command.CommandText = @"
                        UPDATE Jobs SET
                            Name = $name,
                            Description = $description,
                            SourcePath = $sourcePath,
                            DestinationPath = $destinationPath,
                            Enabled = $enabled,
                            ShowNotifications = $showNotifications,
                            Recursive = $recursive,
                            DeleteOrphaned = $deleteOrphaned,
                            VerifyAfterCopy = $verifyAfterCopy,
                            ScheduleEnabled = $scheduleEnabled,
                            AllowedDays = $allowedDays,
                            TimeWindowStart = $timeWindowStart,
                            TimeWindowEnd = $timeWindowEnd,
                            ModifiedAt = $modifiedAt
                        WHERE Id = $id";
                }
                else
                {
                    // Insert new job
                    command.CommandText = @"
                        INSERT INTO Jobs (Id, Name, Description, SourcePath, DestinationPath, Enabled, ShowNotifications,
                                         Recursive, DeleteOrphaned, VerifyAfterCopy, ScheduleEnabled, AllowedDays,
                                         TimeWindowStart, TimeWindowEnd, CreatedAt, ModifiedAt)
                        VALUES ($id, $name, $description, $sourcePath, $destinationPath, $enabled, $showNotifications,
                               $recursive, $deleteOrphaned, $verifyAfterCopy, $scheduleEnabled, $allowedDays,
                               $timeWindowStart, $timeWindowEnd, $createdAt, $modifiedAt)";
                    command.Parameters.AddWithValue("$createdAt", now);
                }

                command.Parameters.AddWithValue("$id", job.Id);
                command.Parameters.AddWithValue("$name", job.Name);
                command.Parameters.AddWithValue("$description", (object?)job.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("$sourcePath", job.SourcePath);
                command.Parameters.AddWithValue("$destinationPath", job.DestinationPath);
                command.Parameters.AddWithValue("$enabled", job.Enabled ? 1 : 0);
                command.Parameters.AddWithValue("$showNotifications", job.ShowNotifications ? 1 : 0);
                command.Parameters.AddWithValue("$recursive", job.Options.Recursive ? 1 : 0);
                command.Parameters.AddWithValue("$deleteOrphaned", job.Options.DeleteOrphaned ? 1 : 0);
                command.Parameters.AddWithValue("$verifyAfterCopy", job.Options.VerifyAfterCopy ? 1 : 0);
                command.Parameters.AddWithValue("$scheduleEnabled", job.Schedule.Enabled ? 1 : 0);
                command.Parameters.AddWithValue("$allowedDays", JsonSerializer.Serialize(job.Schedule.AllowedDays));
                command.Parameters.AddWithValue("$timeWindowStart", (object?)timeWindowStart ?? DBNull.Value);
                command.Parameters.AddWithValue("$timeWindowEnd", (object?)timeWindowEnd ?? DBNull.Value);
                command.Parameters.AddWithValue("$modifiedAt", now);

                await command.ExecuteNonQueryAsync();
                existingIds.Remove(job.Id);
            }

            // Delete jobs that are no longer in the collection
            foreach (var deletedId in existingIds)
            {
                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM Jobs WHERE Id = $id";
                deleteCommand.Parameters.AddWithValue("$id", deletedId);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<long> SaveJobHistoryAsync(BackupJob job, SyncResult result)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO JobHistory (JobId, StartTime, EndTime, Success, FilesCopied, FilesUpdated, FilesDeleted,
                                   FilesSkipped, BytesTransferred, Duration, ErrorCount, StoppedReason)
            VALUES ($jobId, $startTime, $endTime, $success, $filesCopied, $filesUpdated, $filesDeleted,
                   $filesSkipped, $bytesTransferred, $duration, $errorCount, $stoppedReason)";

        var startTime = job.LastRunTime ?? DateTime.Now.AddSeconds(-result.Duration.TotalSeconds);
        
        command.Parameters.AddWithValue("$jobId", job.Id);
        command.Parameters.AddWithValue("$startTime", startTime.ToString("o"));
        command.Parameters.AddWithValue("$endTime", DateTime.Now.ToString("o"));
        command.Parameters.AddWithValue("$success", result.Success ? 1 : 0);
        command.Parameters.AddWithValue("$filesCopied", result.FilesCopied);
        command.Parameters.AddWithValue("$filesUpdated", result.FilesUpdated);
        command.Parameters.AddWithValue("$filesDeleted", result.FilesDeleted);
        command.Parameters.AddWithValue("$filesSkipped", result.FilesSkipped);
        command.Parameters.AddWithValue("$bytesTransferred", result.BytesTransferred);
        command.Parameters.AddWithValue("$duration", result.Duration.ToString());
        command.Parameters.AddWithValue("$errorCount", result.Errors.Count);
        command.Parameters.AddWithValue("$stoppedReason", result.StoppedReason.ToString());

        await command.ExecuteNonQueryAsync();

        // Get the ID of the inserted history record
        command.CommandText = "SELECT last_insert_rowid()";
        var historyId = (long)(await command.ExecuteScalarAsync())!;

        // Save error logs
        foreach (var error in result.Errors)
        {
            var logCommand = connection.CreateCommand();
            logCommand.CommandText = @"
                INSERT INTO JobLogs (JobHistoryId, Timestamp, Level, Message)
                VALUES ($historyId, $timestamp, $level, $message)";
            
            logCommand.Parameters.AddWithValue("$historyId", historyId);
            logCommand.Parameters.AddWithValue("$timestamp", DateTime.Now.ToString("o"));
            logCommand.Parameters.AddWithValue("$level", "Error");
            logCommand.Parameters.AddWithValue("$message", error);
            
            await logCommand.ExecuteNonQueryAsync();
        }

        return historyId;
    }

    public async Task SaveOperationLogAsync(long historyId, SyncOperation operation)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO JobLogs (JobHistoryId, Timestamp, Level, Message)
            VALUES ($historyId, $timestamp, $level, $message)";
        
        var message = operation.Type switch
        {
            SyncOperationType.Copy => $"Copied: {operation.SourcePath} -> {operation.DestinationPath}",
            SyncOperationType.Update => $"Updated: {operation.SourcePath} -> {operation.DestinationPath}",
            SyncOperationType.Delete => $"Deleted: {operation.DestinationPath}",
            SyncOperationType.Skip => $"Skipped: {operation.DestinationPath}",
            _ => operation.Description
        };

        command.Parameters.AddWithValue("$historyId", historyId);
        command.Parameters.AddWithValue("$timestamp", DateTime.Now.ToString("o"));
        command.Parameters.AddWithValue("$level", operation.Type.ToString());
        command.Parameters.AddWithValue("$message", message);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<(long Id, DateTime StartTime, SyncResult Result)>> GetJobHistoryAsync(string jobId, int limit = 50)
    {
        var history = new List<(long, DateTime, SyncResult)>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, StartTime, EndTime, Success, FilesCopied, FilesUpdated, FilesDeleted, FilesSkipped,
                   BytesTransferred, Duration, ErrorCount, StoppedReason
            FROM JobHistory
            WHERE JobId = $jobId
            ORDER BY StartTime DESC
            LIMIT $limit";
        
        command.Parameters.AddWithValue("$jobId", jobId);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var startTime = DateTime.Parse(reader.GetString(1));
            var result = new SyncResult
            {
                Success = reader.GetInt32(3) == 1,
                FilesCopied = reader.GetInt32(4),
                FilesUpdated = reader.GetInt32(5),
                FilesDeleted = reader.GetInt32(6),
                FilesSkipped = reader.GetInt32(7),
                BytesTransferred = reader.GetInt64(8),
                Duration = TimeSpan.Parse(reader.GetString(9)),
                StoppedReason = Enum.Parse<SyncStoppedReason>(reader.GetString(11))
            };

            history.Add((id, startTime, result));
        }

        return history;
    }

    public async Task<List<(DateTime Timestamp, string Level, string Message)>> GetJobLogsAsync(long historyId)
    {
        var logs = new List<(DateTime, string, string)>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Timestamp, Level, Message
            FROM JobLogs
            WHERE JobHistoryId = $historyId
            ORDER BY Timestamp";
        
        command.Parameters.AddWithValue("$historyId", historyId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var timestamp = DateTime.Parse(reader.GetString(0));
            var level = reader.GetString(1);
            var message = reader.GetString(2);
            logs.Add((timestamp, level, message));
        }

        return logs;
    }

    private async Task SaveSettingAsync(SqliteConnection connection, string key, string value)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Settings (Key, Value)
            VALUES ($key, $value)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task MigrateFromJsonAsync(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return;

        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var collection = JsonSerializer.Deserialize<BackupJobCollection>(jsonContent);
            
            if (collection != null)
            {
                await SaveJobsAsync(collection);
                
                // Backup the JSON file
                var backupPath = jsonPath + ".migrated";
                File.Move(jsonPath, backupPath, true);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to migrate from JSON: {ex.Message}", ex);
        }
    }
}
