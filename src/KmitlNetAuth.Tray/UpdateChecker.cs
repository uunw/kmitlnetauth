using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows")]
internal sealed class UpdateChecker : IDisposable
{
    private const string GitHubApiUrl = "https://api.github.com/repos/uunw/kmitlnetauth/releases/latest";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly NotifyIcon _notifyIcon;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly System.Windows.Forms.Timer _timer;
    private string? _releaseUrl;

    public UpdateChecker(NotifyIcon notifyIcon, ILogger logger)
    {
        _notifyIcon = notifyIcon;
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "kmitlnetauth-tray");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        _timer = new System.Windows.Forms.Timer
        {
            Interval = (int)CheckInterval.TotalMilliseconds,
        };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    /// <summary>
    /// Performs an initial check and starts the periodic timer.
    /// </summary>
    public async Task StartAsync()
    {
        // Initial check shortly after startup
        await CheckAsync();
        _timer.Start();
    }

    /// <summary>
    /// Manually trigger an update check. Shows a balloon even if up-to-date.
    /// </summary>
    public async Task CheckManualAsync()
    {
        var hasUpdate = await CheckAsync();
        if (!hasUpdate)
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                "KMITL NetAuth",
                "You are running the latest version.",
                ToolTipIcon.Info);
        }
    }

    /// <returns>True if a newer version was found.</returns>
    private async Task<bool> CheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Update check HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement))
                return false;

            var tagName = tagElement.GetString();
            if (string.IsNullOrEmpty(tagName))
                return false;

            // tag_name is typically "v20260416.1" or "20260416.1"
            var remoteVersion = tagName.TrimStart('v');
            var currentVersion = GetCurrentVersion();

            _logger.LogDebug("Update check: current={Current}, remote={Remote}", currentVersion, remoteVersion);

            if (!IsNewer(currentVersion, remoteVersion))
                return false;

            // Extract release URL for the balloon click handler
            _releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : $"https://github.com/uunw/kmitlnetauth/releases/tag/{tagName}";

            _notifyIcon.BalloonTipClicked -= OnBalloonClicked;
            _notifyIcon.BalloonTipClicked += OnBalloonClicked;

            _notifyIcon.ShowBalloonTip(
                5000,
                "KMITL NetAuth - Update Available",
                $"Version {remoteVersion} is available. Click to download.",
                ToolTipIcon.Info);

            _logger.LogInformation("Update available: {Version}", remoteVersion);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Update check failed: {Error}", ex.Message);
            return false;
        }
    }

    private void OnBalloonClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_releaseUrl))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _releaseUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not open release URL: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Gets the informational version (date-based, e.g. "20260416.1") from the assembly,
    /// falling back to the assembly file version.
    /// </summary>
    private static string GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();

        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVersion))
        {
            // Strip any "+commitsha" suffix
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex >= 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var version = asm.GetName().Version;
        return version != null ? version.ToString() : "0.0.0.1";
    }

    /// <summary>
    /// Compares version strings that follow YYYYMMDD.N format.
    /// Returns true if <paramref name="remote"/> is strictly newer than <paramref name="current"/>.
    /// </summary>
    private static bool IsNewer(string current, string remote)
    {
        // Try parsing as Version (major.minor or major.minor.build.rev)
        if (Version.TryParse(NormalizeVersion(current), out var currentVer) &&
            Version.TryParse(NormalizeVersion(remote), out var remoteVer))
        {
            return remoteVer > currentVer;
        }

        // Fallback: lexicographic comparison (works for date-based strings)
        return string.Compare(remote, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    /// <summary>
    /// Ensures the version string has at least two parts so Version.TryParse succeeds.
    /// "20260416" becomes "20260416.0".
    /// </summary>
    private static string NormalizeVersion(string version)
    {
        return version.Contains('.') ? version : version + ".0";
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _httpClient.Dispose();
    }
}
