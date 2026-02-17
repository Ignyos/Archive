using System.Text.RegularExpressions;

namespace Archive.Core.Sync;

public static class GlobMatcher
{
    public static bool IsMatch(string pattern, string input)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}
