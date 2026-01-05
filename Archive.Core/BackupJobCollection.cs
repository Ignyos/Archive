namespace Archive.Core;

/// <summary>
/// Represents a collection of backup jobs with shared configuration.
/// </summary>
public class BackupJobCollection
{
    /// <summary>
    /// Gets or sets the name of this backup job collection.
    /// </summary>
    public string Name { get; set; } = "Default Collection";

    /// <summary>
    /// Gets or sets the collection of backup jobs.
    /// </summary>
    public List<BackupJob> Jobs { get; set; } = new();

    /// <summary>
    /// Gets or sets the operation schedule that applies to all jobs in this collection.
    /// </summary>
    public OperationSchedule Schedule { get; set; } = new();

    /// <summary>
    /// Gets or sets global settings that can be applied to all jobs.
    /// </summary>
    public Dictionary<string, string> GlobalSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the user has been prompted about running on Windows startup.
    /// </summary>
    public bool HasPromptedForStartup { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the application has been launched before (for first-run experience).
    /// </summary>
    public bool HasLaunchedBefore { get; set; } = false;

    /// <summary>
    /// Gets or sets whether notifications are disabled for all jobs.
    /// </summary>
    public bool DisableAllNotifications { get; set; } = false;

    /// <summary>
    /// Gets the enabled jobs in this collection.
    /// </summary>
    public IEnumerable<BackupJob> EnabledJobs => Jobs.Where(j => j.Enabled);

    /// <summary>
    /// Gets the jobs that are ready to run based on the operation schedule.
    /// </summary>
    /// <returns>Jobs that are enabled and can run according to the schedule.</returns>
    public IEnumerable<BackupJob> GetRunnableJobs()
    {
        return GetRunnableJobs(DateTime.Now);
    }

    /// <summary>
    /// Gets the jobs that are ready to run based on the operation schedule.
    /// </summary>
    /// <param name="currentTime">The current time to check against.</param>
    /// <returns>Jobs that are enabled and can run according to the schedule.</returns>
    public IEnumerable<BackupJob> GetRunnableJobs(DateTime currentTime)
    {
        if (!Schedule.IsOperationAllowed(currentTime))
            return Enumerable.Empty<BackupJob>();

        return EnabledJobs;
    }
}
