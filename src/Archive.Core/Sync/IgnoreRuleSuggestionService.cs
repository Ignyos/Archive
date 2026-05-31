namespace Archive.Core.Sync;

public static class IgnoreRuleSuggestionService
{
    public enum SuggestionScope
    {
        Narrow,
        Medium,
        Broad
    }

    public sealed record Suggestion(
        string Rule,
        string Strategy,
        string Reason,
        SuggestionScope Scope);

    public static IReadOnlyList<Suggestion> BuildSuggestions(
        string sourcePath,
        string failedPath,
        string? errorMessage = null,
        IReadOnlyList<string>? failedPathsInExecution = null)
    {
        if (string.IsNullOrWhiteSpace(failedPath))
        {
            return Array.Empty<Suggestion>();
        }

        var suggestions = new List<Suggestion>();
        var seenRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetRelativePath(sourcePath, failedPath, out var relativePath))
        {
            if (IsLikelyDirectoryPath(failedPath, errorMessage))
            {
                AddRule(
                    suggestions,
                    seenRules,
                    EnsureDirectoryRule(relativePath),
                    "Ignore this folder",
                    "Ignore everything in the folder where this error happened.",
                    SuggestionScope.Medium);
            }
            else
            {
                AddRule(
                    suggestions,
                    seenRules,
                    relativePath,
                    "Ignore exact path",
                    "Ignore only this exact path.",
                    SuggestionScope.Narrow);

                var parentDirectory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    AddRule(
                        suggestions,
                        seenRules,
                        EnsureDirectoryRule(parentDirectory),
                        "Ignore parent folder",
                        "Ignore everything in the parent folder where this error happened.",
                        SuggestionScope.Medium);
                }
            }

            var fileName = Path.GetFileName(relativePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                AddRule(
                    suggestions,
                    seenRules,
                    fileName,
                    "Ignore filename anywhere",
                    "Ignore this file name anywhere in the source tree.",
                    SuggestionScope.Medium);

                var extension = Path.GetExtension(fileName);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    AddRule(
                        suggestions,
                        seenRules,
                        $"*{extension}",
                        "Ignore by extension",
                        "Ignore files with this extension across the source tree.",
                        SuggestionScope.Broad);
                }
            }
        }
        else
        {
            var normalizedFailedPath = failedPath.Replace('\\', '/').Trim();
            var fileName = Path.GetFileName(normalizedFailedPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                AddRule(
                    suggestions,
                    seenRules,
                    fileName,
                    "Ignore filename anywhere",
                    "Ignore this file name anywhere in the source tree.",
                    SuggestionScope.Medium);
            }
        }

        AddExecutionClusterSuggestions(
            suggestions,
            seenRules,
            sourcePath,
            failedPathsInExecution ?? Array.Empty<string>());

        return suggestions;
    }

    private static void AddRule(
        ICollection<Suggestion> suggestions,
        ISet<string> seenRules,
        string? rule,
        string strategy,
        string reason,
        SuggestionScope scope)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return;
        }

        var normalized = rule.Trim().Replace('\\', '/');
        if (!seenRules.Add(normalized))
        {
            return;
        }

        suggestions.Add(new Suggestion(normalized, strategy, reason, scope));
    }

    private static void AddExecutionClusterSuggestions(
        ICollection<Suggestion> suggestions,
        ISet<string> seenRules,
        string sourcePath,
        IReadOnlyList<string> failedPaths)
    {
        if (failedPaths.Count < 2)
        {
            return;
        }

        var relatives = failedPaths
            .Select(path => TryGetRelativePath(sourcePath, path, out var rel) ? rel : string.Empty)
            .Where(rel => !string.IsNullOrWhiteSpace(rel))
            .ToList();

        if (relatives.Count < 2)
        {
            return;
        }

        var extensionGroup = relatives
            .Select(Path.GetExtension)
            .Where(ext => !string.IsNullOrWhiteSpace(ext))
            .GroupBy(ext => ext!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (extensionGroup is not null && extensionGroup.Count() >= 2)
        {
            AddRule(
                suggestions,
                seenRules,
                $"*{extensionGroup.Key}",
                "Ignore failed extension pattern",
                "Ignore files matching the extension pattern found in multiple failures from this run.",
                SuggestionScope.Broad);
        }

        var folderGroup = relatives
            .Select(rel => Path.GetDirectoryName(rel)?.Replace('\\', '/'))
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .GroupBy(folder => folder!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (folderGroup is not null && folderGroup.Count() >= 2)
        {
            AddRule(
                suggestions,
                seenRules,
                EnsureDirectoryRule(folderGroup.Key),
                "Ignore failed folder pattern",
                "Ignore files in the folder where multiple failures happened in this run.",
                SuggestionScope.Medium);
        }
    }

    private static bool TryGetRelativePath(string sourcePath, string failedPath, out string relativePath)
    {
        relativePath = string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(failedPath))
        {
            return false;
        }

        string sourceFullPath;
        string failedFullPath;

        try
        {
            sourceFullPath = Path.GetFullPath(sourcePath.Trim()).TrimEnd('\\', '/');
            failedFullPath = Path.GetFullPath(failedPath.Trim()).TrimEnd('\\', '/');
        }
        catch
        {
            return false;
        }

        if (string.Equals(sourceFullPath, failedFullPath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = Path.GetFileName(failedFullPath);
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        if (!failedFullPath.StartsWith(sourceFullPath + "\\", StringComparison.OrdinalIgnoreCase)
            && !failedFullPath.StartsWith(sourceFullPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = Path.GetRelativePath(sourceFullPath, failedFullPath).Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(relativePath)
            && !relativePath.StartsWith("..", StringComparison.Ordinal);
    }

    private static bool IsLikelyDirectoryPath(string failedPath, string? errorMessage)
    {
        var normalizedPath = failedPath.Replace('\\', '/').Trim();
        if (normalizedPath.EndsWith('/'))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(errorMessage)
            && errorMessage.Contains("directory", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureDirectoryRule(string value)
    {
        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        return normalized + "/";
    }
}