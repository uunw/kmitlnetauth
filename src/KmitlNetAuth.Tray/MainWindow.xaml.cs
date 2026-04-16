using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Services;
using KmitlNetAuth.Tray.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using WinForms = System.Windows.Forms;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _services;
    private readonly string _configPath;
    private readonly IAuthService _authService;
    private readonly ILogger<MainWindow> _logger;
    private readonly UpdateChecker _updateChecker;
    private readonly WinForms.NotifyIcon _notifyIcon;

    // Cached pages so state is preserved across navigation
    private readonly bool _navigateToSettings;

    private DashboardPage? _dashboardPage;
    private LogPage? _logPage;
    private SettingsPage? _settingsPage;
    private DebugPage? _debugPage;
    private AboutPage? _aboutPage;

    public MainWindow(IServiceProvider services, string configPath, bool navigateToSettings = false)
    {
        InitializeComponent();

        _services = services;
        _configPath = configPath;
        _navigateToSettings = navigateToSettings;
        _authService = services.GetRequiredService<IAuthService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();
        _updateChecker = new UpdateChecker(_logger);

        // Build tray icon
        var trayIcon = SystemIcons.Application;
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var extracted = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
            if (extracted != null)
                trayIcon = extracted;
        }

        var showItem = new WinForms.ToolStripMenuItem("Show / Hide");
        showItem.Click += (_, _) => ToggleVisibility();

        var quitItem = new WinForms.ToolStripMenuItem("Quit");
        quitItem.Click += OnQuitClicked;

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add(quitItem);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "KMITL NetAuth",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();

        // Subscribe to status changes for balloon tips
        _authService.StatusChanged += OnStatusChanged;

        // Auto-update check on startup
        _ = _updateChecker.StartAsync();

        // Navigate to appropriate page on load
        Loaded += (_, _) => NavigateToTag(_navigateToSettings ? "settings" : "dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag)
            NavigateToTag(tag);
    }

    private void NavigateToTag(string tag)
    {
        object? page = tag switch
        {
            "dashboard" => _dashboardPage ??= new DashboardPage(_services),
            "log" => _logPage ??= new LogPage(),
            "settings" => _settingsPage ??= new SettingsPage(_services, _configPath),
            "debug" => _debugPage ??= new DebugPage(_services, _configPath),
            "about" => _aboutPage ??= new AboutPage(_updateChecker),
            _ => null,
        };

        if (page != null)
            PageFrame.Navigate(page);
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
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

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _updateChecker.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }
}
