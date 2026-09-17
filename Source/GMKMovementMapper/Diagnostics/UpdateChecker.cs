using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace GMKMovementMapper.Diagnostics;

/// <summary>
/// Compares the running build against the latest GitHub release of the
/// configured repository. Best-effort only: no network, no releases
/// published yet, or a parse failure all just mean "no update to report" —
/// this must never interrupt startup or show an error for something this
/// minor.
/// </summary>
public static class UpdateChecker
{
    private const string RepoOwner = "thedawnpirate1-spec";
    private const string RepoName = "GMK-Movement-Mapper";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>
    /// Returns the latest release's tag and its version if it's newer than the
    /// running build, or null if there's nothing newer (or the check failed
    /// for any reason). Runs entirely on a background thread; safe to call
    /// from the UI thread and await/fire-and-forget without blocking it.
    /// </summary>
    public static async Task<(string Tag, Version Version)?> CheckForNewerReleaseAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GMKMovementMapper", CurrentVersion.ToString()));
            var response = await client.GetAsync($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
            if (!response.IsSuccessStatusCode) return null;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = json.RootElement.GetProperty("tag_name").GetString();
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var digits = new string(tag.TrimStart('v', 'V').TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            if (!Version.TryParse(digits, out var latest)) return null;

            return latest > CurrentVersion ? (tag, latest) : null;
        }
        catch
        {
            return null;
        }
    }
}
