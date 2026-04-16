using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray.Pages;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class SettingsPage : Page
{
    private readonly Config _config;
    private readonly string _configPath;
    private readonly ICredentialStore? _credentialStore;
    private readonly IAutoStartManager _autoStartManager;
    private readonly ILogger<SettingsPage> _logger;

    public SettingsPage(IServiceProvider services, string configPath)
    {
        InitializeComponent();

        _config = services.GetRequiredService<Config>();
        _configPath = configPath;
        _credentialStore = services.GetService<ICredentialStore>();
        _autoStartManager = services.GetRequiredService<IAutoStartManager>();
        _logger = services.GetRequiredService<ILogger<SettingsPage>>();

        PopulateFields();
    }

    private void PopulateFields()
    {
        // Auth
        UsernameBox.Text = _config.Username;
        PasswordBox.Password = _config.GetPassword(_credentialStore);
        IpAddressBox.Text = _config.IpAddress ?? "";

        // Network
        PortalUrlBox.Text = _config.PortalUrl;
        HeartbeatUrlBox.Text = _config.HeartbeatUrl;
        InternetCheckUrlBox.Text = _config.InternetCheckUrl;
        TimeoutBox.Value = _config.Timeout;
        AcceptInvalidCertsToggle.IsChecked = _config.AcceptInvalidCerts;

        // Service
        IntervalBox.Value = _config.Interval;
        MaxAttemptBox.Value = _config.MaxAttempt;
        BackoffIntervalBox.Value = _config.BackoffInterval;
        AutoLoginToggle.IsChecked = _config.AutoLogin;

        // Auto Start
        AutoStartToggle.IsChecked = _autoStartManager.IsEnabled;
        AutoStartStatus.Text = _autoStartManager.IsEnabled ? "Currently enabled" : "Currently disabled";

        // Logging
        SelectLogLevel(_config.LogLevel);
        LogRetentionBox.Value = _config.LogRetentionDays;

        // Misc
        NotificationsToggle.IsChecked = _config.NotificationsEnabled;
        StartMinimizedToggle.IsChecked = _config.StartMinimized;
        AutoUpdateToggle.IsChecked = _config.AutoUpdateCheck;
    }

    private void SelectLogLevel(string level)
    {
        foreach (ComboBoxItem item in LogLevelBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), level, StringComparison.OrdinalIgnoreCase))
            {
                LogLevelBox.SelectedItem = item;
                return;
            }
        }
        // Default to Information
        LogLevelBox.SelectedIndex = 2;
    }

    private void OnAutoStartChanged(object sender, RoutedEventArgs e)
    {
        if (AutoStartToggle.IsChecked == true)
        {
            var exePath = Environment.ProcessPath
                ?? System.IO.Path.Combine(AppContext.BaseDirectory, "kmitlnetauth.exe");
            _autoStartManager.Enable(exePath);
            AutoStartStatus.Text = "Currently enabled";
        }
        else
        {
            _autoStartManager.Disable();
            AutoStartStatus.Text = "Currently disabled";
        }

        _logger.LogInformation("Auto start {State}", AutoStartToggle.IsChecked == true ? "enabled" : "disabled");
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        // Validate username
        if (string.IsNullOrWhiteSpace(UsernameBox.Text))
        {
            UsernameError.Visibility = Visibility.Visible;
            return;
        }
        UsernameError.Visibility = Visibility.Collapsed;

        // Auth
        _config.Username = UsernameBox.Text.Trim();
        _config.Password = PasswordBox.Password;
        _config.IpAddress = string.IsNullOrWhiteSpace(IpAddressBox.Text)
            ? null
            : IpAddressBox.Text.Trim();

        // Network
        _config.PortalUrl = PortalUrlBox.Text.Trim();
        _config.HeartbeatUrl = HeartbeatUrlBox.Text.Trim();
        _config.InternetCheckUrl = InternetCheckUrlBox.Text.Trim();
        _config.Timeout = (int)(TimeoutBox.Value ?? 10);
        _config.AcceptInvalidCerts = AcceptInvalidCertsToggle.IsChecked == true;

        // Service
        _config.Interval = (ulong)(IntervalBox.Value ?? 300);
        _config.MaxAttempt = (uint)(MaxAttemptBox.Value ?? 20);
        _config.BackoffInterval = (int)(BackoffIntervalBox.Value ?? 60);
        _config.AutoLogin = AutoLoginToggle.IsChecked == true;

        // Logging
        if (LogLevelBox.SelectedItem is ComboBoxItem levelItem)
            _config.LogLevel = levelItem.Content?.ToString() ?? "Information";
        _config.LogRetentionDays = (int)(LogRetentionBox.Value ?? 30);

        // Misc
        _config.NotificationsEnabled = NotificationsToggle.IsChecked == true;
        _config.StartMinimized = StartMinimizedToggle.IsChecked == true;
        _config.AutoUpdateCheck = AutoUpdateToggle.IsChecked == true;

        try
        {
            _config.Save(_configPath, _credentialStore);
            SaveStatus.Text = "Saved";
            _logger.LogInformation("Settings saved");
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "Save failed";
            _logger.LogError(ex, "Failed to save settings");
        }
    }
}
