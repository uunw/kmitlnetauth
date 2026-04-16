using System.Runtime.Versioning;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using KmitlNetAuth.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows")]
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Config _config;
    private readonly string _configPath;
    private readonly ICredentialStore? _credentialStore;
    private readonly IAutoStartManager _autoStartManager;
    private readonly IAuthService _authService;
    private readonly ILogger<TrayApplicationContext> _logger;
    private readonly UpdateChecker _updateChecker;

    private readonly ToolStripMenuItem _autoLoginItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _showConsoleItem;
    private readonly ToolStripMenuItem[] _logLevelItems;

    public TrayApplicationContext(IServiceProvider services, string configPath)
    {
        _config = services.GetRequiredService<Config>();
        _configPath = configPath;
        _credentialStore = services.GetService<ICredentialStore>();
        _autoStartManager = services.GetRequiredService<IAutoStartManager>();
        _authService = services.GetRequiredService<IAuthService>();
        _logger = services.GetRequiredService<ILogger<TrayApplicationContext>>();

        // Auto Login toggle
        _autoLoginItem = new ToolStripMenuItem("Auto Login")
        {
            CheckOnClick = true,
            Checked = _config.AutoLogin,
        };
        _autoLoginItem.CheckedChanged += OnAutoLoginChanged;

        // Auto Start toggle
        _autoStartItem = new ToolStripMenuItem("Auto Start")
        {
            CheckOnClick = true,
            Checked = _autoStartManager.IsEnabled,
        };
        _autoStartItem.CheckedChanged += OnAutoStartChanged;

        // Settings
        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += OnSettingsClicked;

        // Show Console toggle
        _showConsoleItem = new ToolStripMenuItem("Show Console")
        {
            CheckOnClick = true,
            Checked = false,
        };
        _showConsoleItem.CheckedChanged += OnShowConsoleChanged;

        // Log Level submenu
        var logLevels = new[] { "Error", "Warning", "Information", "Debug", "Verbose" };
        _logLevelItems = logLevels.Select(level =>
        {
            var item = new ToolStripMenuItem(level)
            {
                Tag = level,
                Checked = string.Equals(_config.LogLevel, level, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += OnLogLevelChanged;
            return item;
        }).ToArray();

        var logLevelMenu = new ToolStripMenuItem("Log Level");
        logLevelMenu.DropDownItems.AddRange(_logLevelItems);

        // Check for Updates
        var updateItem = new ToolStripMenuItem("Check for Updates");
        updateItem.Click += OnCheckForUpdatesClicked;

        // Quit
        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += OnQuitClicked;

        // Context menu
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_autoLoginItem);
        contextMenu.Items.Add(_autoStartItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(_showConsoleItem);
        contextMenu.Items.Add(logLevelMenu);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(updateItem);
        contextMenu.Items.Add(quitItem);

        // Tray icon
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "KMITL NetAuth",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };

        // Subscribe to status changes for balloon tips
        _authService.StatusChanged += OnStatusChanged;

        // Auto-update checker (fire-and-forget on startup)
        _updateChecker = new UpdateChecker(_notifyIcon, _logger);
        _ = _updateChecker.StartAsync();
    }

    private void OnStatusChanged(object? sender, AuthStatusChangedEventArgs e)
    {
        var (title, body) = e.NewStatus switch
        {
            AuthStatus.Online => ("Connected", "Internet connection is active."),
            AuthStatus.Offline => ("Disconnected", "Internet connection lost."),
            AuthStatus.Paused => ("Paused", "Auto-login is disabled."),
            _ => ((string?)null, (string?)null),
        };

        if (title != null && body != null)
        {
            _notifyIcon.ShowBalloonTip(3000, $"KMITL NetAuth - {title}", body, ToolTipIcon.Info);
        }
    }

    private void OnAutoLoginChanged(object? sender, EventArgs e)
    {
        _config.AutoLogin = _autoLoginItem.Checked;
        SaveConfig();
        _logger.LogInformation("Auto login {State}", _config.AutoLogin ? "enabled" : "disabled");
    }

    private void OnAutoStartChanged(object? sender, EventArgs e)
    {
        if (_autoStartItem.Checked)
        {
            var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            _autoStartManager.Enable(exePath);
        }
        else
        {
            _autoStartManager.Disable();
        }

        _logger.LogInformation("Auto start {State}", _autoStartItem.Checked ? "enabled" : "disabled");
    }

    private void OnLogLevelChanged(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem clicked)
            return;

        foreach (var item in _logLevelItems)
            item.Checked = item == clicked;

        _config.LogLevel = (string)clicked.Tag!;
        SaveConfig();
        _logger.LogInformation("Log level changed to {Level}", _config.LogLevel);
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_config, _configPath, _credentialStore);
        form.ShowDialog();
    }

    private void OnShowConsoleChanged(object? sender, EventArgs e)
    {
        if (_showConsoleItem.Checked)
        {
            NativeConsole.Show();
            _logger.LogInformation("Console window shown");
        }
        else
        {
            NativeConsole.Hide();
            _logger.LogInformation("Console window hidden");
        }
    }

    private async void OnCheckForUpdatesClicked(object? sender, EventArgs e)
    {
        await _updateChecker.CheckManualAsync();
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private void SaveConfig()
    {
        try
        {
            _config.Save(_configPath, _credentialStore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save config");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateChecker.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
