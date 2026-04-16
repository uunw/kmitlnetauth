using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace KmitlNetAuth.Tray.Pages.Setup;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class CredentialsPage : Page
{
    public CredentialsPage()
    {
        InitializeComponent();
    }

    /// <summary>Current value of the username field (trimmed).</summary>
    public string Username => UsernameBox.Text?.Trim() ?? string.Empty;

    /// <summary>Current value of the password field.</summary>
    public string Password => PasswordBox.Password ?? string.Empty;

    /// <summary>
    /// Current value of the IP address field, or null if empty.
    /// </summary>
    public string? IpAddress
    {
        get
        {
            var raw = IpAddressBox.Text?.Trim();
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
    }

    /// <summary>
    /// Validates required fields (username + password). Shows inline error
    /// messages for any invalid fields and returns true only if all required
    /// fields are filled.
    /// </summary>
    public bool Validate()
    {
        var usernameOk = !string.IsNullOrWhiteSpace(UsernameBox.Text);
        var passwordOk = !string.IsNullOrWhiteSpace(PasswordBox.Password);

        UsernameError.Visibility = usernameOk ? Visibility.Collapsed : Visibility.Visible;
        PasswordError.Visibility = passwordOk ? Visibility.Collapsed : Visibility.Visible;

        if (!usernameOk)
            UsernameBox.Focus();
        else if (!passwordOk)
            PasswordBox.Focus();

        return usernameOk && passwordOk;
    }

    /// <summary>Move keyboard focus to the first empty required field.</summary>
    public void FocusFirstField()
    {
        if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            UsernameBox.Focus();
        else if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            PasswordBox.Focus();
        else
            IpAddressBox.Focus();
    }
}
