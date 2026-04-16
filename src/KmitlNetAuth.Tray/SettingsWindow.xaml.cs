using System.Runtime.Versioning;
using System.Windows;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using Wpf.Ui.Controls;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class SettingsWindow : FluentWindow
{
    private readonly Config _config;
    private readonly string _configPath;
    private readonly ICredentialStore? _credentialStore;

    public SettingsWindow(Config config, string configPath, ICredentialStore? credentialStore)
    {
        _config = config;
        _configPath = configPath;
        _credentialStore = credentialStore;

        InitializeComponent();

        // Populate fields from config
        UsernameBox.Text = config.Username;
        PasswordBox.Password = config.GetPassword(credentialStore);
        IpAddressBox.Text = config.IpAddress ?? "";
        IntervalBox.Value = config.Interval;
        AutoLoginToggle.IsChecked = config.AutoLogin;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        _config.Username = UsernameBox.Text.Trim();
        _config.Password = PasswordBox.Password;
        _config.IpAddress = string.IsNullOrWhiteSpace(IpAddressBox.Text)
            ? null
            : IpAddressBox.Text.Trim();
        _config.Interval = (ulong)(IntervalBox.Value ?? 300);
        _config.AutoLogin = AutoLoginToggle.IsChecked == true;

        _config.Save(_configPath, _credentialStore);

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
