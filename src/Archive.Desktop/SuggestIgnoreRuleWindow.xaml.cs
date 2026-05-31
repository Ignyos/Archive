using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Archive.Core.Sync;

namespace Archive.Desktop;

public partial class SuggestIgnoreRuleWindow : Window
{
    private const int EstimateScanLimit = 20000;

    public ObservableCollection<SuggestionRow> Suggestions { get; } = [];

    public IgnoreRuleSuggestionService.Suggestion? SelectedSuggestion { get; private set; }

    public SuggestIgnoreRuleWindow(
        string sourcePath,
        string failedPath,
        string? errorMessage,
        IReadOnlyList<string> failedPathsInExecution)
    {
        InitializeComponent();

        SuggestionsDataGrid.ItemsSource = Suggestions;
        ContextTextBlock.Text = $"Failed path: {failedPath}";

        var suggestions = IgnoreRuleSuggestionService.BuildSuggestions(
            sourcePath,
            failedPath,
            errorMessage,
            failedPathsInExecution);

        foreach (var suggestion in suggestions)
        {
            var estimate = EstimateMatches(sourcePath, suggestion.Rule);
            Suggestions.Add(new SuggestionRow
            {
                Suggestion = suggestion,
                Rule = suggestion.Rule,
                Strategy = suggestion.Strategy,
                Reason = suggestion.Reason,
                ScopeText = suggestion.Scope.ToString(),
                EstimatedMatchesText = FormatEstimatedMatches(estimate)
            });
        }

        if (Suggestions.Count > 0)
        {
            SuggestionsDataGrid.SelectedIndex = 0;
        }
        else
        {
            SelectionSummaryTextBlock.Text = "No suggestions are available for this failed item.";
        }
    }

    private void SuggestionsDataGrid_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SuggestionsDataGrid.SelectedItem is not SuggestionRow row)
        {
            SelectedSuggestion = null;
            SelectionSummaryTextBlock.Text = "Select a rule to continue.";
            return;
        }

        SelectedSuggestion = row.Suggestion;
        SelectionSummaryTextBlock.Text =
            $"Selected rule: {row.Rule}\nStrategy: {row.Strategy}\nScope: {row.ScopeText}\nEstimated impact: {row.EstimatedMatchesText}\nReason: {row.Reason}";
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedSuggestion is null)
        {
            System.Windows.MessageBox.Show(
                "Select a rule first.",
                "Suggest Ignore Rule",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string FormatEstimatedMatches((int MatchCount, bool Truncated) estimate)
    {
        return estimate.Truncated
            ? $">= {estimate.MatchCount}"
            : estimate.MatchCount.ToString();
    }

    private static (int MatchCount, bool Truncated) EstimateMatches(string sourcePath, string rule)
    {
        var normalizedRule = IgnoreRuleMatcher.NormalizeRules([rule]);
        if (normalizedRule.Count == 0)
        {
            return (0, false);
        }

        var matcherRule = normalizedRule[0];

        if (File.Exists(sourcePath))
        {
            var fileName = Path.GetFileName(sourcePath);
            return (IgnoreRuleMatcher.IsIgnored(fileName, [matcherRule]) ? 1 : 0, false);
        }

        if (!Directory.Exists(sourcePath))
        {
            return (0, false);
        }

        var matches = 0;
        var scanned = 0;
        var truncated = false;

        foreach (var fullPath in EnumerateFilesSafe(sourcePath))
        {
            scanned++;
            if (scanned > EstimateScanLimit)
            {
                truncated = true;
                break;
            }

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(sourcePath, fullPath);
            }
            catch
            {
                continue;
            }

            if (IgnoreRuleMatcher.IsIgnored(relativePath, [matcherRule]))
            {
                matches++;
            }
        }

        return (matches, truncated);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
    {
        IEnumerator<string>? enumerator;
        try
        {
            enumerator = Directory
                .EnumerateFiles(rootPath, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                })
                .GetEnumerator();
        }
        catch
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = enumerator.MoveNext();
                }
                catch
                {
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                yield return enumerator.Current;
            }
        }
    }

    public sealed class SuggestionRow
    {
        public IgnoreRuleSuggestionService.Suggestion Suggestion { get; init; } = null!;

        public string Rule { get; init; } = string.Empty;

        public string Strategy { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public string ScopeText { get; init; } = string.Empty;

        public string EstimatedMatchesText { get; init; } = string.Empty;
    }
}
