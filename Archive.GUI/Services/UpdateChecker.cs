using System.Net.Http;
using System.Text.Json;

namespace Archive.GUI.Services;

/// <summary>
/// Checks for application updates from GitHub releases.
/// </summary>
public class UpdateChecker
{
    private const string GitHubApiUrl = "https://api.github.com/repos/ignyos/archive/releases/latest";
    private const string CurrentVersion = "1.0.0"; // TODO: Get from assembly version
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static UpdateChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Archive-UpdateChecker");
    }

    /// <summary>
    /// Checks if a newer version is available.
    /// </summary>
    /// <returns>Update information if available, null otherwise.</returns>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null || string.IsNullOrEmpty(release.tag_name))
                return null;

            // Remove 'v' prefix if present
            var latestVersion = release.tag_name.TrimStart('v');
            
            if (IsNewerVersion(CurrentVersion, latestVersion))
            {
                return new UpdateInfo
                {
                    Version = latestVersion,
                    ReleaseUrl = release.html_url ?? string.Empty,
                    ReleaseNotes = release.body ?? string.Empty,
                    PublishedAt = release.published_at
                };
            }

            return null;
        }
        catch
        {
            // Silently fail - update checks are not critical
            return null;
        }
    }

    private bool IsNewerVersion(string current, string latest)
    {
        try
        {
            var currentParts = current.Split('.').Select(int.Parse).ToArray();
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(currentParts.Length, latestParts.Length); i++)
            {
                if (latestParts[i] > currentParts[i])
                    return true;
                if (latestParts[i] < currentParts[i])
                    return false;
            }

            return latestParts.Length > currentParts.Length;
        }
        catch
        {
            return false;
        }
    }

    private class GitHubRelease
    {
        public string? tag_name { get; set; }
        public string? name { get; set; }
        public string? body { get; set; }
        public string? html_url { get; set; }
        public DateTime published_at { get; set; }
    }
}

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}
