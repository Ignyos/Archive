using System.Collections.ObjectModel;
using System.Windows;
using Archive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Archive.Desktop;

public partial class ApplicationLogsWindow : Window
{
    private const int MaxRows = 500;

    public ObservableCollection<ApplicationLogRow> Items { get; } = [];

    public ApplicationLogsWindow()
    {
        InitializeComponent();
        LogsDataGrid.ItemsSource = Items;
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

            var rows = await dbContext.ApplicationLogs
                .AsNoTracking()
                .OrderByDescending(x => x.TimestampUtc)
                .Take(MaxRows)
                .Select(x => new ApplicationLogRow
                {
                    TimestampLocal = x.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    Level = x.Level,
                    SourceContext = string.IsNullOrWhiteSpace(x.SourceContext) ? "-" : x.SourceContext,
                    Message = x.Message,
                    ExceptionSummary = string.IsNullOrWhiteSpace(x.Exception)
                        ? "-"
                        : GetFirstLine(x.Exception)
                })
                .ToListAsync();

            Items.Clear();
            foreach (var row in rows)
            {
                Items.Add(row);
            }
        }
        catch
        {
            System.Windows.MessageBox.Show(
                "Unable to load application logs.",
                "Archive",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static string GetFirstLine(string text)
    {
        var index = text.IndexOf('\n');
        if (index < 0)
        {
            return text;
        }

        return text[..index].TrimEnd('\r');
    }

    public sealed class ApplicationLogRow
    {
        public string TimestampLocal { get; init; } = string.Empty;

        public string Level { get; init; } = string.Empty;

        public string SourceContext { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public string ExceptionSummary { get; init; } = string.Empty;
    }
}