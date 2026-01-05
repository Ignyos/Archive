namespace Archive.Core;

/// <summary>
/// Defines time-based constraints for when backup operations can be executed.
/// </summary>
public class OperationSchedule
{
    /// <summary>
    /// Gets or sets a value indicating whether schedule restrictions are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the days of the week when operations are allowed.
    /// </summary>
    public List<DayOfWeek> AllowedDays { get; set; } = new()
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    };

    /// <summary>
    /// Gets or sets the time windows during which operations are allowed.
    /// </summary>
    public List<TimeWindow> AllowedTimeWindows { get; set; } = new();

    /// <summary>
    /// Determines whether operations are allowed at the specified date and time.
    /// </summary>
    /// <param name="dateTime">The date and time to check.</param>
    /// <returns>True if operations are allowed; otherwise, false.</returns>
    public bool IsOperationAllowed(DateTime dateTime)
    {
        if (!Enabled)
            return true;

        // Check if the day is allowed
        if (!AllowedDays.Contains(dateTime.DayOfWeek))
            return false;

        // If no time windows are specified, allow all times on allowed days
        if (AllowedTimeWindows.Count == 0)
            return true;

        // Check if the time falls within any allowed window
        var timeOfDay = dateTime.TimeOfDay;
        return AllowedTimeWindows.Any(window => window.IsTimeInWindow(timeOfDay));
    }

    /// <summary>
    /// Gets the next available time when operations are allowed.
    /// </summary>
    /// <param name="fromDateTime">The starting date and time to search from.</param>
    /// <returns>The next allowed date and time, or null if schedule is not enabled.</returns>
    public DateTime? GetNextAllowedTime(DateTime fromDateTime)
    {
        if (!Enabled)
            return fromDateTime;

        var checkDate = fromDateTime;
        var maxDaysToCheck = 14; // Prevent infinite loops

        for (int i = 0; i < maxDaysToCheck; i++)
        {
            if (AllowedDays.Contains(checkDate.DayOfWeek))
            {
                if (AllowedTimeWindows.Count == 0)
                    return checkDate.Date;

                foreach (var window in AllowedTimeWindows.OrderBy(w => w.StartTime))
                {
                    var windowStart = checkDate.Date.Add(window.StartTime);
                    if (windowStart > fromDateTime)
                        return windowStart;
                }
            }

            checkDate = checkDate.Date.AddDays(1);
        }

        return null;
    }
}

/// <summary>
/// Represents a time window during the day when operations are allowed.
/// </summary>
public class TimeWindow
{
    /// <summary>
    /// Gets or sets the start time of the window.
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the window.
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Gets or sets whether this window has a cutoff time.
    /// If false, jobs are allowed to run indefinitely after the start time.
    /// </summary>
    public bool HasCutoff { get; set; } = true;

    /// <summary>
    /// Determines whether the specified time falls within this window.
    /// </summary>
    /// <param name="time">The time to check.</param>
    /// <returns>True if the time is within the window; otherwise, false.</returns>
    public bool IsTimeInWindow(TimeSpan time)
    {
        // If there's no cutoff, just check if we're past the start time
        if (!HasCutoff)
        {
            return time >= StartTime;
        }

        if (EndTime > StartTime)
        {
            // Normal case: window doesn't cross midnight
            return time >= StartTime && time <= EndTime;
        }
        else
        {
            // Window crosses midnight (e.g., 22:00 to 06:00)
            return time >= StartTime || time <= EndTime;
        }
    }

    /// <summary>
    /// Creates a time window with the specified hours.
    /// </summary>
    /// <param name="startHour">The starting hour (0-23).</param>
    /// <param name="endHour">The ending hour (0-23).</param>
    /// <returns>A new TimeWindow instance.</returns>
    public static TimeWindow FromHours(int startHour, int endHour)
    {
        return new TimeWindow
        {
            StartTime = TimeSpan.FromHours(startHour),
            EndTime = TimeSpan.FromHours(endHour)
        };
    }
}
