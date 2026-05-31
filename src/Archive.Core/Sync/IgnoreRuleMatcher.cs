namespace Archive.Core.Sync;

public static class IgnoreRuleMatcher
{
    public static IReadOnlyList<string> NormalizeRules(IEnumerable<string?> rules)
    {
        return rules
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Where(x => x.Length > 0 && !x.StartsWith('#'))
            .ToArray();
    }

    public static bool IsIgnored(string relativePath, IReadOnlyList<string> rules)
    {
        if (rules.Count == 0)
        {
            return false;
        }

        var normalizedPath = NormalizePath(relativePath);
        if (normalizedPath.Length == 0)
        {
            return false;
        }

        var isIgnored = false;

        foreach (var rawRule in rules)
        {
            if (string.IsNullOrWhiteSpace(rawRule))
            {
                continue;
            }

            var isNegated = rawRule[0] == '!';
            var rule = isNegated ? rawRule[1..].Trim() : rawRule;
            if (rule.Length == 0)
            {
                continue;
            }

            if (RuleMatchesPath(rule, normalizedPath))
            {
                isIgnored = !isNegated;
            }
        }

        return isIgnored;
    }

    private static bool RuleMatchesPath(string rule, string normalizedPath)
    {
        var normalizedRule = NormalizePattern(rule);
        if (normalizedRule.Length == 0)
        {
            return false;
        }

        if (normalizedRule.EndsWith('/'))
        {
            normalizedRule += "**";
        }

        if (!normalizedRule.Contains('/'))
        {
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (GlobMatcher.IsMatch(normalizedRule, segment))
                {
                    return true;
                }
            }

            return false;
        }

        if (GlobMatcher.IsMatch(normalizedRule, normalizedPath))
        {
            return true;
        }

        return GlobMatcher.IsMatch($"**/{normalizedRule}", normalizedPath);
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/')
            .TrimEnd('/');
    }

    private static string NormalizePattern(string pattern)
    {
        return pattern
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/');
    }
}
