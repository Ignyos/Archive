namespace Archive.Desktop.Tests;

public class RecurringCronModeServiceTests
{
    [Fact]
    public void BuildDailyCron_ReturnsExpectedExpression()
    {
        var cron = RecurringCronModeService.BuildDailyCron("02:30");

        Assert.Equal("0 30 2 * * ?", cron);
    }

    [Fact]
    public void BuildWeeklyCron_ReturnsExpectedExpression()
    {
        var cron = RecurringCronModeService.BuildWeeklyCron(DayOfWeek.Monday, "06:15");

        Assert.Equal("0 15 6 ? * MON", cron);
    }

    [Fact]
    public void BuildMonthlyCron_ReturnsExpectedExpression()
    {
        var cron = RecurringCronModeService.BuildMonthlyCron(12, "23:45");

        Assert.Equal("0 45 23 12 * ?", cron);
    }

    [Fact]
    public void TryParseSimpleRecurring_ReturnsDailyConfig_WhenDailyPattern()
    {
        var parsed = RecurringCronModeService.TryParseSimpleRecurring(
            "0 0 2 * * ?",
            out var config);

        Assert.True(parsed);
        Assert.NotNull(config);
        Assert.Equal(SimpleRecurringFrequency.Daily, config!.Frequency);
        Assert.Equal("02:00", config.TimeOfDayText);
    }

    [Fact]
    public void TryParseSimpleRecurring_ReturnsFalse_WhenUnsupportedPattern()
    {
        var parsed = RecurringCronModeService.TryParseSimpleRecurring(
            "0 0 */6 * * ?",
            out var _);

        Assert.False(parsed);
    }
}
