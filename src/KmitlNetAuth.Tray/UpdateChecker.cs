using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class UpdateChecker : IDisposable
{
    private const string GitHubApiUrl = "https://api.github.com/repos/uunw/kmitlnetauth/releases/latest";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly System.Timers.Timer _timer;

    public UpdateChecker(ILogger logger)
    {
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "KmitlNetAuth");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        _timer = new System.Timers.Timer(CheckInterval.TotalMilliseconds);
        _timer.Elapsed += async (_, _) =>
        {
            var (hasUpdate, _, remoteVersion, msiUrl) = await CheckAsync();
            if (hasUpdate)
            {
                _logger.LogInformation("Update available: {Version}", remoteVersion);
            }
        };
    }

    /// <summary>
    /// Performs an initial check and starts the periodic timer.
    /// </summary>
    public async Task StartAsync()
    {
        await CheckAsync();
        _timer.Start();
    }

    /// <summary>
    /// Checks for updates. Returns structured result instead of showing UI.
    /// </summary>
    /// <returns>Tuple of (hasUpdate, currentVersion, remoteVersion, msiDownloadUrl).</returns>
    public async Task<(bool HasUpdate, string CurrentVersion, string? RemoteVersion, string? MsiUrl)> CheckAsync()
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Update check HTTP {StatusCode}", response.StatusCode);
                return (false, currentVersion, null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement))
                return (false, currentVersion, null, null);

            var tagName = tagElement.GetString();
            if (string.IsNullOrEmpty(tagName))
                return (false, currentVersion, null, null);

            var remoteVersion = tagName.TrimStart('v');

            _logger.LogDebug("Update check: current={Current}, remote={Remote}", currentVersion, remoteVersion);

            if (!IsNewer(currentVersion, remoteVersion))
                return (false, currentVersion, remoteVersion, null);

            // Find MSI asset URL from release assets
            string? msiUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameEl)
                        ? nameEl.GetString() : null;
                    if (name != null && name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        msiUrl = asset.TryGetProperty("browser_download_url", out var urlEl)
                            ? urlEl.GetString() : null;
                        break;
                    }
                }
            }

            _logger.LogInformation("Update available: {Version} (MSI: {HasMsi})", remoteVersion, msiUrl != null);
            return (true, currentVersion, remoteVersion, msiUrl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Update check failed: {Error}", ex.Message);
            return (false, currentVersion, null, null);
        }
    }

    /// <summary>
    /// Downloads the MSI installer and launches it, then shuts down the application.
    /// </summary>
    public async Task DownloadAndInstallAsync(string msiUrl, IProgress<double> progress)
    {
        try
        {
            var tempDir = Path.GetTempPath();
            var msiPath = Path.Combine(tempDir, "kmitlnetauth-update.msi");

            _logger.LogInformation("Downloading update from {Url}", msiUrl);

            using var response = await _httpClient.GetAsync(msiUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var bytesRead = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(msiPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            int read;
            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;

                if (totalBytes > 0)
                {
                    var percent = (double)bytesRead / totalBytes * 100;
                    progress.Report(percent);
                }
            }

            progress.Report(100);
            _logger.LogInformation("Download complete, launching installer");

            Process.Start("msiexec", $"/i \"{msiPath}\" /passive");
            WpfApplication.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or install update");
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
