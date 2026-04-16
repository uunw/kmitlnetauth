using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray.Pages;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class DebugPage : Page
{
    private readonly AuthClient _authClient;
    private readonly Config _config;
    private readonly ICredentialStore? _credentialStore;
    private readonly INetworkInfo _networkInfo;
    private readonly ILogger<DebugPage> _logger;
    private readonly string _configPath;

    public DebugPage(IServiceProvider services, string configPath)
    {
        InitializeComponent();

        _authClient = services.GetRequiredService<AuthClient>();
        _config = services.GetRequiredService<Config>();
        _credentialStore = services.GetService<ICredentialStore>();
        _networkInfo = services.GetRequiredService<INetworkInfo>();
        _logger = services.GetRequiredService<ILogger<DebugPage>>();
        _configPath = configPath;

        Loaded += (_, _) => LoadDebugInfo();
    }

    private void LoadDebugInfo()
    {
        ConfigPathText.Text = _configPath;

        // Credential store status
        var hasPassword = !string.IsNullOrEmpty(_config.GetPassword(_credentialStore));
        CredentialStoreText.Text = _credentialStore != null
            ? $"{_credentialStore.GetType().Name} (password {(hasPassword ? "stored" : "not found")})"
            : "Not available";

        // Network info
        MacAddressText.Text = _networkInfo.GetMacAddress();

        var (isDhcp, currentIp) = DhcpDetector.GetNetworkStatus();
        CurrentIpText.Text = string.IsNullOrEmpty(currentIp) ? "(unknown)" : currentIp;
        DhcpStatusText.Text = isDhcp ? "DHCP enabled" : "Static / not DHCP";

        // Endpoints
        EndpointsText.Text =
            $"Portal:         {_config.PortalUrl}\n" +
            $"Heartbeat:      {_config.HeartbeatUrl}\n" +
            $"Internet Check: {_config.InternetCheckUrl}";

        // Raw config
        try
        {
            if (File.Exists(_configPath))
                RawConfigText.Text = File.ReadAllText(_configPath);
            else
                RawConfigText.Text = "(config file does not exist yet)";
        }
        catch (Exception ex)
        {
            RawConfigText.Text = $"Error reading config: {ex.Message}";
        }
    }

    private async void OnTestLoginClicked(object sender, RoutedEventArgs e)
    {
        TestLoginButton.IsEnabled = false;
        TestResultText.Text = "Testing login...";
        try
        {
            var result = await _authClient.LoginAsync();
            TestResultText.Text = result ? "Login: SUCCESS" : "Login: FAILED";
        }
        catch (Exception ex)
        {
            TestResultText.Text = $"Login: ERROR - {ex.Message}";
        }
        finally
        {
            TestLoginButton.IsEnabled = true;
        }
    }

    private async void OnTestHeartbeatClicked(object sender, RoutedEventArgs e)
    {
        TestHeartbeatButton.IsEnabled = false;
        TestResultText.Text = "Testing heartbeat...";
        try
        {
            var result = await _authClient.HeartbeatAsync();
            TestResultText.Text = result ? "Heartbeat: SUCCESS" : "Heartbeat: FAILED";
        }
        catch (Exception ex)
        {
            TestResultText.Text = $"Heartbeat: ERROR - {ex.Message}";
        }
        finally
        {
            TestHeartbeatButton.IsEnabled = true;
        }
    }

    private async void OnTestInternetClicked(object sender, RoutedEventArgs e)
    {
        TestInternetButton.IsEnabled = false;
        TestResultText.Text = "Testing internet...";
        try
        {
            var result = await _authClient.CheckInternetAsync();
            TestResultText.Text = result ? "Internet: CONNECTED" : "Internet: NOT CONNECTED";
        }
        catch (Exception ex)
        {
            TestResultText.Text = $"Internet: ERROR - {ex.Message}";
        }
        finally
        {
            TestInternetButton.IsEnabled = true;
        }
    }
}
