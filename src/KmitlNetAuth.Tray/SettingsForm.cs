using System.Runtime.Versioning;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows")]
internal sealed class SettingsForm : Form
{
    private readonly Config _config;
    private readonly string _configPath;
    private readonly ICredentialStore? _credentialStore;

    private readonly TextBox _usernameBox;
    private readonly TextBox _passwordBox;
    private readonly TextBox _ipAddressBox;
    private readonly NumericUpDown _intervalUpDown;
    private readonly CheckBox _autoLoginCheck;

    public SettingsForm(Config config, string configPath, ICredentialStore? credentialStore)
    {
        _config = config;
        _configPath = configPath;
        _credentialStore = credentialStore;

        Text = "KMITL NetAuth Settings";
        Size = new Size(400, 350);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var yOffset = 15;
        const int labelX = 20;
        const int inputX = 130;
        const int inputWidth = 230;
        const int rowHeight = 35;

        // Username
        var usernameLabel = new Label
        {
            Text = "Username:",
            Location = new Point(labelX, yOffset + 3),
            AutoSize = true,
        };
        _usernameBox = new TextBox
        {
            Location = new Point(inputX, yOffset),
            Width = inputWidth,
            Text = config.Username,
        };
        yOffset += rowHeight;

        // Password
        var passwordLabel = new Label
        {
            Text = "Password:",
            Location = new Point(labelX, yOffset + 3),
            AutoSize = true,
        };
        _passwordBox = new TextBox
        {
            Location = new Point(inputX, yOffset),
            Width = inputWidth,
            PasswordChar = '*',
            Text = config.GetPassword(credentialStore),
        };
        yOffset += rowHeight;

        // IP Address
        var ipLabel = new Label
        {
            Text = "IP Address:",
            Location = new Point(labelX, yOffset + 3),
            AutoSize = true,
        };
        _ipAddressBox = new TextBox
        {
            Location = new Point(inputX, yOffset),
            Width = inputWidth,
            Text = config.IpAddress ?? "",
            PlaceholderText = "(auto-detect)",
        };
        yOffset += rowHeight;

        // Interval
        var intervalLabel = new Label
        {
            Text = "Interval (sec):",
            Location = new Point(labelX, yOffset + 3),
            AutoSize = true,
        };
        _intervalUpDown = new NumericUpDown
        {
            Location = new Point(inputX, yOffset),
            Width = inputWidth,
            Minimum = 10,
            Maximum = 86400,
            Value = Math.Clamp((decimal)config.Interval, 10, 86400),
        };
        yOffset += rowHeight;

        // Auto Login
        _autoLoginCheck = new CheckBox
        {
            Text = "Auto Login",
            Location = new Point(inputX, yOffset),
            Checked = config.AutoLogin,
            AutoSize = true,
        };
        yOffset += rowHeight + 15;

        // Buttons
        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(inputX, yOffset),
            Width = 100,
            DialogResult = DialogResult.OK,
        };
        saveButton.Click += OnSaveClicked;
        AcceptButton = saveButton;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(inputX + 115, yOffset),
            Width = 100,
            DialogResult = DialogResult.Cancel,
        };
        CancelButton = cancelButton;

        Controls.AddRange([
            usernameLabel, _usernameBox,
            passwordLabel, _passwordBox,
            ipLabel, _ipAddressBox,
            intervalLabel, _intervalUpDown,
            _autoLoginCheck,
            saveButton, cancelButton,
        ]);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        _config.Username = _usernameBox.Text.Trim();
        _config.Password = _passwordBox.Text;
        _config.IpAddress = string.IsNullOrWhiteSpace(_ipAddressBox.Text) ? null : _ipAddressBox.Text.Trim();
        _config.Interval = (ulong)_intervalUpDown.Value;
        _config.AutoLogin = _autoLoginCheck.Checked;

        _config.Save(_configPath, _credentialStore);

        DialogResult = DialogResult.OK;
        Close();
    }
}
