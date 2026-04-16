using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using KmitlNetAuth.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using WinForms = System.Windows.Forms;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class MainWindow : FluentWindow
{
    private readonly Config _config;
    private readonly string _configPath;
    private readonly ICredentialStore? _credentialStore;
    private readonly IAutoStartManager _autoStartManager;
    private readonly IAuthService _authService;
    private readonly ILogger<MainWindow> _logger;
    private readonly UpdateChecker _updateChecker;

    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _autoLoginItem;
    private readonly WinForms.ToolStripMenuItem _autoStartItem;
    private readonly WinForms.ToolStripMenuItem _showConsoleItem;
    private readonly WinForms.ToolStripMenuItem[] _logLevelItems;

    public MainWindow(IServiceProvider services, string configPath)
    {
        InitializeComponent();

        _config = services.GetRequiredService<Config>();
        _configPath = configPath;
        _credentialStore = services.GetService<ICredentialStore>();
        _autoStartManager = services.GetRequiredService<IAutoStartManager>();
        _authService = services.GetRequiredService<IAuthService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();
        _updateChecker = new UpdateChecker(_logger);

        // Auto Login toggle
        _autoLoginItem = new WinForms.ToolStripMenuItem("Auto Login")
        {
            CheckOnClick = true,
            Checked = _config.AutoLogin,
        };
        _autoLoginItem.CheckedChanged += OnAutoLoginChanged;

        // Auto Start toggle
        _autoStartItem = new WinForms.ToolStripMenuItem("Auto Start")
        {
            CheckOnClick = true,
            Checked = _autoStartManager.IsEnabled,
        };
        _autoStartItem.CheckedChanged += OnAutoStartChanged;

        // Settings
        var settingsItem = new WinForms.ToolStripMenuItem("Settings");
        settingsItem.Click += OnSettingsClicked;

        // Show Console toggle
        _showConsoleItem = new WinForms.ToolStripMenuItem("Show Console")
        {
            CheckOnClick = true,
            Checked = false,
        };
        _showConsoleItem.CheckedChanged += OnShowConsoleChanged;

        // Log Level submenu
        var logLevels = new[] { "Error", "Warning", "Information", "Debug", "Verbose" };
        _logLevelItems = logLevels.Select(level =>
        {
            var item = new WinForms.ToolStripMenuItem(level)
            {
                Tag = level,
                Checked = string.Equals(_config.LogLevel, level, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += OnLogLevelChanged;
            return item;
        }).ToArray();

        var logLevelMenu = new WinForms.ToolStripMenuItem("Log Level");
        logLevelMenu.DropDownItems.AddRange(_logLevelItems);

        // Check for Updates
        var updateItem = new WinForms.ToolStripMenuItem("Check for Updates");
        updateItem.Click += OnCheckForUpdatesClicked;

        // Quit
        var quitItem = new WinForms.ToolStripMenuItem("Quit");
        quitItem.Click += OnQuitClicked;

        // Context menu
        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add(_autoLoginItem);
        contextMenu.Items.Add(_autoStartItem);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(_showConsoleItem);
        contextMenu.Items.Add(logLevelMenu);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add(updateItem);
        contextMenu.Items.Add(quitItem);

        // Tray icon using System.Windows.Forms.NotifyIcon
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "KMITL NetAuth",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };

        // Subscribe to status changes for balloon tips
        _authService.StatusChanged += OnStatusChanged;

        // Auto-update check on startup (fire-and-forget)
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
            _notifyIcon.ShowBalloonTip(
                3000,
                $"KMITL NetAuth - {title}",
                body,
                WinForms.ToolTipIcon.Info);
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
            var exePath = Environment.ProcessPath
                ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            _autoStartManager.Enable(exePath);
        }
        else
        {
            _autoStartManager.Disable();
        }

        _logger.LogInformation("Auto start {State}", _autoStartItem.Checked ? "enabled" : "disabled");
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var settingsWindow = new SettingsWindow(_config, _configPath, _credentialStore);
            settingsWindow.ShowDialog();
        });
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

    private void OnLogLevelChanged(object? sender, EventArgs e)
    {
        if (sender is not WinForms.ToolStripMenuItem clicked)
            return;

        foreach (var item in _logLevelItems)
            item.Checked = item == clicked;

        _config.LogLevel = (string)clicked.Tag!;
        SaveConfig();
        _logger.LogInformation("Log level changed to {Level}", _config.LogLevel);
    }

    private async void OnCheckForUpdatesClicked(object? sender, EventArgs e)
    {
        var (hasUpdate, currentVersion, remoteVersion, msiUrl) = await _updateChecker.CheckAsync();
        if (hasUpdate && !string.IsNullOrEmpty(remoteVersion))
        {
            Dispatcher.Invoke(() =>
            {
                var updateWindow = new UpdateWindow(
                    currentVersion, remoteVersion, msiUrl, _updateChecker);
                updateWindow.ShowDialog();
            });
        }
        else
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                "KMITL NetAuth",
                "You are running the latest version.",
                WinForms.ToolTipIcon.Info);
        }
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _updateChecker.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Prevent closing; minimize to tray instead
        e.Cancel = true;
        Visibility = Visibility.Hidden;
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
}
