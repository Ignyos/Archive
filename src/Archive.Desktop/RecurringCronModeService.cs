using System.Globalization;
using System.Text.RegularExpressions;

namespace Archive.Desktop;

public enum RecurringScheduleMode
{
    Simple,
    Advanced
}

public enum SimpleRecurringFrequency
{
    Daily,
    Weekly,
    Monthly
}

public sealed class SimpleRecurringConfig
{
    public SimpleRecurringFrequency Frequency { get; init; }

    public DayOfWeek DayOfWeek { get; init; }

    public int DayOfMonth { get; init; } = 1;

    public string TimeOfDayText { get; init; } = "02:00";
}

public static class RecurringCronModeService
{
    private static readonly Regex DailyPattern = new("^0\\s+(?<m>\\d{1,2})\\s+(?<h>\\d{1,2})\\s+\\*\\s+\\*\\s+\\?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WeeklyPattern = new("^0\\s+(?<m>\\d{1,2})\\s+(?<h>\\d{1,2})\\s+\\?\\s+\\*\\s+(?<dow>SUN|MON|TUE|WED|THU|FRI|SAT)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MonthlyPattern = new("^0\\s+(?<m>\\d{1,2})\\s+(?<h>\\d{1,2})\\s+(?<dom>\\d{1,2})\\s+\\*\\s+\\?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string BuildDailyCron(string timeOfDayText)
    {
        var (hour, minute) = ParseTimeOfDay(timeOfDayText);
        return $"0 {minute} {hour} * * ?";
    }

    public static string BuildWeeklyCron(DayOfWeek dayOfWeek, string timeOfDayText)
    {
        var (hour, minute) = ParseTimeOfDay(timeOfDayText);
        return $"0 {minute} {hour} ? * {ToQuartzDayOfWeek(dayOfWeek)}";
    }

    public static string BuildMonthlyCron(int dayOfMonth, string timeOfDayText)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31.");
        }

        var (hour, minute) = ParseTimeOfDay(timeOfDayText);
        return $"0 {minute} {hour} {dayOfMonth} * ?";
    }

    public static bool TryParseSimpleRecurring(string cronExpression, out SimpleRecurringConfig? config)
    {
        config = null;
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        var cron = cronExpression.Trim();

        var dailyMatch = DailyPattern.Match(cron);
        if (dailyMatch.Success)
        {
            config = new SimpleRecurringConfig
            {
                Frequency = SimpleRecurringFrequency.Daily,
                TimeOfDayText = FormatTime(dailyMatch.Groups["h"].Value, dailyMatch.Groups["m"].Value)
            };
            return true;
        }

        var weeklyMatch = WeeklyPattern.Match(cron);
        if (weeklyMatch.Success)
        {
            if (!TryParseQuartzDayOfWeek(weeklyMatch.Groups["dow"].Value, out var dayOfWeek))
            {
                return false;
            }

            config = new SimpleRecurringConfig
            {
                Frequency = SimpleRecurringFrequency.Weekly,
                DayOfWeek = dayOfWeek,
                TimeOfDayText = FormatTime(weeklyMatch.Groups["h"].Value, weeklyMatch.Groups["m"].Value)
            };
            return true;
        }

        var monthlyMatch = MonthlyPattern.Match(cron);
        if (monthlyMatch.Success)
        {
            var dayOfMonth = int.Parse(monthlyMatch.Groups["dom"].Value, CultureInfo.InvariantCulture);
            config = new SimpleRecurringConfig
            {
                Frequency = SimpleRecurringFrequency.Monthly,
                DayOfMonth = dayOfMonth,
                TimeOfDayText = FormatTime(monthlyMatch.Groups["h"].Value, monthlyMatch.Groups["m"].Value)
            };
            return true;
        }

        return false;
    }

    private static (int Hour, int Minute) ParseTimeOfDay(string timeOfDayText)
    {
        if (!TimeSpan.TryParseExact(timeOfDayText.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var time))
        {
            throw new FormatException("Time must be in HH:mm format.");
        }

        return (time.Hours, time.Minutes);
    }

    private static string FormatTime(string hour, string minute)
    {
        var h = int.Parse(hour, CultureInfo.InvariantCulture);
        var m = int.Parse(minute, CultureInfo.InvariantCulture);
        return $"{h:00}:{m:00}";
    }

    private static string ToQuartzDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "SUN",
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            DayOfWeek.Saturday => "SAT",
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek))
        };
    }

    private static bool TryParseQuartzDayOfWeek(string token, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = token.ToUpperInvariant() switch
        {
            "SUN" => DayOfWeek.Sunday,
            "MON" => DayOfWeek.Monday,
            "TUE" => DayOfWeek.Tuesday,
            "WED" => DayOfWeek.Wednesday,
            "THU" => DayOfWeek.Thursday,
            "FRI" => DayOfWeek.Friday,
            "SAT" => DayOfWeek.Saturday,
            _ => (DayOfWeek)(-1)
        };

        return dayOfWeek != (DayOfWeek)(-1);
    }
}
