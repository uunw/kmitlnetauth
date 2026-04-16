using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using KmitlNetAuth.Core;
using Serilog;
using MessageBox = System.Windows.MessageBox;

namespace KmitlNetAuth.Tray.Pages;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class AboutPage : Page
{
    private readonly UpdateChecker _updateChecker;
    private readonly string _logDir;
    private string? _pendingMsiUrl;

    public AboutPage(UpdateChecker updateChecker)
    {
        InitializeComponent();

        _updateChecker = updateChecker;
        _logDir = ConfigPaths.GetLogDirectory();

        var version = GetCurrentVersion();
        VersionText.Text = $"Version {version}";
        UpdateStatusText.Text = "Click the button to check for updates.";
        LogDirText.Text = _logDir;
    }

    private void OnOpenLogFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ensure the folder exists before asking Explorer to open it,
            // otherwise the user gets a confusing "path not found" dialog on
            // a clean machine that has not emitted any logs yet.
            Directory.CreateDirectory(_logDir);

            Process.Start(new ProcessStartInfo(_logDir)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open log folder {Path}", _logDir);
            MessageBox.Show(
                $"Could not open log folder:\n{_logDir}\n\n{ex.Message}",
                "KMITL NetAuth",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void OnCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking for updates...";
        InstallUpdateButton.Visibility = Visibility.Collapsed;
        UpdateVersionText.Visibility = Visibility.Collapsed;

        try
        {
            var (hasUpdate, currentVersion, remoteVersion, msiUrl) = await _updateChecker.CheckAsync();

            if (hasUpdate && !string.IsNullOrEmpty(remoteVersion))
            {
                UpdateStatusText.Text = "A new version is available!";
                UpdateVersionText.Text = $"Current: {currentVersion}  ->  New: {remoteVersion}";
                UpdateVersionText.Visibility = Visibility.Visible;

                if (!string.IsNullOrEmpty(msiUrl))
                {
                    _pendingMsiUrl = msiUrl;
                    InstallUpdateButton.Visibility = Visibility.Visible;
                }
            }
            else
            {
                UpdateStatusText.Text = "You are running the latest version.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Check failed: {ex.Message}";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingMsiUrl))
            return;

        InstallUpdateButton.IsEnabled = false;
        CheckUpdateButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        ProgressText.Visibility = Visibility.Visible;
        UpdateStatusText.Text = "Downloading update...";

        var progress = new Progress<double>(percent =>
        {
            DownloadProgress.Value = percent;
            ProgressText.Text = $"{percent:F0}%";
        });

        await _updateChecker.DownloadAndInstallAsync(_pendingMsiUrl, progress);
    }

    private static string GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex >= 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var version = asm.GetName().Version;
        return version != null ? version.ToString() : "0.0.0.1";
    }
}
