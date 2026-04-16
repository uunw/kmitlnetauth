using System.Runtime.Versioning;
using System.Windows.Controls;

namespace KmitlNetAuth.Tray.Pages.Setup;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class ConfirmPage : Page
{
    public ConfirmPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Populate the review summary. Password is always masked.
    /// </summary>
    public void SetSummary(string username, string password, string? ipAddress)
    {
        UsernameSummary.Text = string.IsNullOrWhiteSpace(username) ? "-" : username;

        // Mask: always show 8 bullets regardless of real length so actual
        // length cannot be inferred from the UI.
        PasswordSummary.Text = string.IsNullOrEmpty(password) ? "-" : new string('\u2022', 8);

        IpSummary.Text = string.IsNullOrWhiteSpace(ipAddress) ? "Auto-detect" : ipAddress;
    }
}
