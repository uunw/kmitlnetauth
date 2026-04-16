using System.Runtime.Versioning;
using System.Windows;
using Wpf.Ui.Controls;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class UpdateWindow : FluentWindow
{
    private readonly string? _msiUrl;
    private readonly UpdateChecker _updateChecker;

    public UpdateWindow(
        string currentVersion,
        string remoteVersion,
        string? msiUrl,
        UpdateChecker updateChecker)
    {
        _msiUrl = msiUrl;
        _updateChecker = updateChecker;

        InitializeComponent();

        CurrentVersionText.Text = $"Current version: {currentVersion}";
        NewVersionText.Text = $"New version: {remoteVersion}";

        // Disable Update button if no MSI download URL is available
        if (string.IsNullOrEmpty(msiUrl))
        {
            UpdateButton.IsEnabled = false;
            UpdateButton.Content = "No installer available";
        }
    }

    private async void OnUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_msiUrl))
            return;

        UpdateButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        ProgressText.Visibility = Visibility.Visible;

        var progress = new Progress<double>(percent =>
        {
            DownloadProgress.Value = percent;
            ProgressText.Text = $"{percent:F0}%";
        });

        await _updateChecker.DownloadAndInstallAsync(_msiUrl, progress);
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
