using System.Windows.Threading;
using Archive.Core;

namespace Archive.GUI.Services;

/// <summary>
/// Handles scheduling and execution of backup jobs.
/// </summary>
public class SchedulerService
{
    private readonly DatabaseService _databaseService;
    private readonly DispatcherTimer _timer;
    private BackupJobCollection? _collection;
    private bool _isRunning;
    private readonly Dictionary<string, CancellationTokenSource> _runningJobs = new();
    private readonly UpdateChecker _updateChecker = new();
    private DateTime _lastUpdateCheck = DateTime.MinValue;

    public event EventHandler<JobStartedEventArgs>? JobStarted;
    public event EventHandler<JobCompletedEventArgs>? JobCompleted;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<UpdateInfo>? UpdateAvailable;

    public SchedulerService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Check every 30 seconds for better accuracy
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Starts the scheduler.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
            return;

        _collection = await _databaseService.LoadJobsAsync();
        _isRunning = true;
        _timer.Start();
        StatusChanged?.Invoke(this, "Scheduler started");
        
        // Check for updates on startup if last check was more than 24 hours ago
        await CheckForUpdatesIfNeededAsync();
    }

    /// <summary>
    /// Stops the scheduler.
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
        _isRunning = false;
        StatusChanged?.Invoke(this, "Scheduler stopped");
    }

    /// <summary>
    /// Reloads the job collection from disk.
    /// </summary>
    public async Task ReloadJobsAsync()
    {
        _collection = await _databaseService.LoadJobsAsync();
        StatusChanged?.Invoke(this, "Jobs reloaded");
    }

    /// <summary>
    /// Runs a specific job immediately.
    /// </summary>
    public async Task RunJobAsync(BackupJob job)
    {
        // Prevent running the same job multiple times
        if (job.IsRunning)
        {
            StatusChanged?.Invoke(this, $"Job '{job.Name}' is already running");
            return;
        }

        await ExecuteJobAsync(job);
    }

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    public void CancelJob(BackupJob job)
    {
        if (_runningJobs.TryGetValue(job.Id, out var cts))
        {
            cts.Cancel();
            StatusChanged?.Invoke(this, $"Cancelling job: {job.Name}");
        }
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_collection == null)
            return;

        var now = DateTime.Now;
        
        // Check for updates once per day
        await CheckForUpdatesIfNeededAsync();
        
        var runnableJobs = _collection.GetRunnableJobs(now).ToList();

        foreach (var job in runnableJobs)
        {
            // Check if job should run based on schedule
            if (ShouldRunJob(job, now))
            {
                await ExecuteJobAsync(job);
            }
        }
    }

    private bool ShouldRunJob(BackupJob job, DateTime now)
    {
        // If job was run before and we have a next run time, check if it's due
        if (job.NextRunTime.HasValue && now >= job.NextRunTime.Value)
            return true;

        // If no next run time set yet
        if (!job.NextRunTime.HasValue)
        {
            // If schedule is enabled, calculate next run time
            if (job.Schedule.Enabled && job.Schedule.AllowedTimeWindows.Any())
            {
                var nextRun = CalculateNextRunTime(job, now);
                if (nextRun.HasValue && now >= nextRun.Value)
                    return true;
            }
            else if (job.LastRunTime == null)
            {
                // No schedule and never run before - run it now
                return true;
            }
        }

        return false;
    }

    private DateTime? CalculateNextRunTime(BackupJob job, DateTime fromTime)
    {
        if (!job.Schedule.Enabled || !job.Schedule.AllowedTimeWindows.Any())
            return null;

        var currentDate = fromTime.Date;
        var currentTime = fromTime.TimeOfDay;

        // Check today's time windows
        if (job.Schedule.AllowedDays.Contains(fromTime.DayOfWeek))
        {
            foreach (var window in job.Schedule.AllowedTimeWindows.OrderBy(w => w.StartTime))
            {
                if (currentTime < window.StartTime)
                {
                    return currentDate.Add(window.StartTime);
                }
            }
        }

        // No suitable time today, find next allowed day
        for (int daysAhead = 1; daysAhead <= 7; daysAhead++)
        {
            var checkDate = currentDate.AddDays(daysAhead);
            if (job.Schedule.AllowedDays.Contains(checkDate.DayOfWeek))
            {
                var firstWindow = job.Schedule.AllowedTimeWindows.OrderBy(w => w.StartTime).First();
                return checkDate.Add(firstWindow.StartTime);
            }
        }

        return null;
    }

    private async Task ExecuteJobAsync(BackupJob job)
    {
        Console.WriteLine($"[SchedulerService] ExecuteJobAsync started for: {job.Name}");
        
        // Create cancellation token for this job
        var cts = new CancellationTokenSource();
        _runningJobs[job.Id] = cts;
        
        // Mark job as running
        job.IsRunning = true;
        
        JobStarted?.Invoke(this, new JobStartedEventArgs(job));
        StatusChanged?.Invoke(this, $"Running job: {job.Name}");

        long? historyId = null;

        try
        {
            var engine = new SyncEngine();
            
            // Prepare to collect operations for logging
            var operations = new List<SyncOperation>();
            
            // Set up operation logger to collect operations
            job.Options.OperationLogger = (operation) =>
            {
                operations.Add(operation);
            };
            
            // Set up schedule callback to stop job if it exceeds the end time
            job.Options.ShouldContinue = () => 
            {
                if (!job.Schedule.Enabled || !job.Schedule.AllowedTimeWindows.Any())
                    return true;

                var now = DateTime.Now.TimeOfDay;
                // Check if current time is past any of the time window end times
                foreach (var window in job.Schedule.AllowedTimeWindows)
                {
                    if (window.IsTimeInWindow(now))
                        return true;
                }
                
                // Past the end time, stop the job
                return false;
            };

            Console.WriteLine($"[SchedulerService] Starting sync for: {job.Name}");
            var result = await engine.SynchronizeAsync(
                job.SourcePath,
                job.DestinationPath,
                job.Options,
                cts.Token);

            Console.WriteLine($"[SchedulerService] Sync completed. Success: {result.Success}");

            // Update job with result
            job.LastRunTime = DateTime.Now;
            job.LastResult = result;

            // Calculate next run time
            job.NextRunTime = CalculateNextRunTime(job, DateTime.Now);

            // Save updated collection and history
            if (_collection != null)
            {
                await _databaseService.SaveJobsAsync(_collection);
                
                // Save history and get the history ID
                historyId = await _databaseService.SaveJobHistoryAsync(job, result);
                
                // Save operation logs if we have a history ID
                if (historyId.HasValue)
                {
                    foreach (var operation in operations)
                    {
                        await _databaseService.SaveOperationLogAsync(historyId.Value, operation);
                    }
                }
            }

            Console.WriteLine($"[SchedulerService] Invoking JobCompleted event");
            JobCompleted?.Invoke(this, new JobCompletedEventArgs(job, result));
            StatusChanged?.Invoke(this, $"Completed job: {job.Name}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[SchedulerService] Job cancelled: {job.Name}");
            var cancelledResult = new SyncResult
            {
                Success = false,
                StoppedReason = SyncStoppedReason.Cancelled
            };

            job.LastResult = cancelledResult;
            JobCompleted?.Invoke(this, new JobCompletedEventArgs(job, cancelledResult));
            StatusChanged?.Invoke(this, $"Cancelled job: {job.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SchedulerService] Exception: {ex.Message}");
            var errorResult = new SyncResult
            {
                Success = false,
                StoppedReason = SyncStoppedReason.Error
            };
            errorResult.Errors.Add(ex.Message);

            job.LastResult = errorResult;
            JobCompleted?.Invoke(this, new JobCompletedEventArgs(job, errorResult));
            StatusChanged?.Invoke(this, $"Failed job: {job.Name} - {ex.Message}");
        }
        finally
        {
            // Clean up cancellation token and mark job as not running
            _runningJobs.Remove(job.Id);
            job.IsRunning = false;
        }
    }

    private async Task CheckForUpdatesIfNeededAsync()
    {
        // Check if we need to check for updates (once per 24 hours)
        if ((DateTime.Now - _lastUpdateCheck).TotalHours < 24)
            return;

        _lastUpdateCheck = DateTime.Now;

        try
        {
            var updateInfo = await _updateChecker.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                UpdateAvailable?.Invoke(this, updateInfo);
            }
        }
        catch
        {
            // Silently fail - update checks are not critical
        }
    }
}

public class JobStartedEventArgs : EventArgs
{
    public BackupJob Job { get; }
    public JobStartedEventArgs(BackupJob job) => Job = job;
}

public class JobCompletedEventArgs : EventArgs
{
    public BackupJob Job { get; }
    public SyncResult Result { get; }
    
    public JobCompletedEventArgs(BackupJob job, SyncResult result)
    {
        Job = job;
        Result = result;
    }
}
