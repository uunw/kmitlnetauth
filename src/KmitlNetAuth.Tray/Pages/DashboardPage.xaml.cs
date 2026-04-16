using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray.Pages;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class DashboardPage : Page
{
    private readonly IAuthService _authService;
    private readonly AuthClient _authClient;
    private readonly Config _config;
    private readonly ILogger<DashboardPage> _logger;
    private readonly DispatcherTimer _uptimeTimer;
    private readonly DateTime _startTime;
    private DateTime _lastStatusChange;

    public DashboardPage(IServiceProvider services)
    {
        InitializeComponent();

        _authService = services.GetRequiredService<IAuthService>();
        _authClient = services.GetRequiredService<AuthClient>();
        _config = services.GetRequiredService<Config>();
        _logger = services.GetRequiredService<ILogger<DashboardPage>>();
        _startTime = Process.GetCurrentProcess().StartTime;
        _lastStatusChange = DateTime.Now;

        // Populate static info
        UsernameText.Text = string.IsNullOrEmpty(_config.Username) ? "(not set)" : _config.Username;
        IpAddressText.Text = string.IsNullOrEmpty(_config.IpAddress) ? "(auto-detect)" : _config.IpAddress;

        // Set initial status
        UpdateStatusDisplay(_authService.CurrentStatus);
        UpdatePauseButton();

        // Subscribe to real-time status changes
        _authService.StatusChanged += OnStatusChanged;

        // Uptime ticker
        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) =>
        {
            var uptime = DateTime.Now - _startTime;
            UptimeText.Text = FormatUptime(uptime);
        };
        _uptimeTimer.Start();

        Unloaded += (_, _) =>
        {
            _uptimeTimer.Stop();
            _authService.StatusChanged -= OnStatusChanged;
        };
    }

    private void OnStatusChanged(object? sender, AuthStatusChangedEventArgs e)
    {
        _lastStatusChange = DateTime.Now;
        Dispatcher.Invoke(() =>
        {
            UpdateStatusDisplay(e.NewStatus);
            UpdatePauseButton();
        });
    }

    private void UpdateStatusDisplay(AuthStatus status)
    {
        StatusText.Text = status.ToString();
        LastChangeText.Text = _lastStatusChange.ToString("HH:mm:ss");

        StatusIndicator.Fill = status switch
        {
            AuthStatus.Online => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0xCC, 0x71)),    // green
            AuthStatus.Offline => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C)),    // red
            AuthStatus.Connecting => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0x9C, 0x12)),  // amber
            AuthStatus.Paused => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x95, 0xA5, 0xA6)),      // gray
            _ => new SolidColorBrush(Colors.Gray),
        };
    }

    private void UpdatePauseButton()
    {
        PauseButton.Content = _config.AutoLogin ? "Pause" : "Resume";
    }

    private async void OnLoginNowClicked(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        LoginButton.Content = "Logging in...";
        try
        {
            await _authClient.LoginAsync();
            _logger.LogInformation("Manual login triggered from dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual login failed");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Login Now";
        }
    }

    private void OnPauseResumeClicked(object sender, RoutedEventArgs e)
    {
        _config.AutoLogin = !_config.AutoLogin;
        UpdatePauseButton();
        _logger.LogInformation("Auto-login {State} from dashboard", _config.AutoLogin ? "resumed" : "paused");
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }
}
