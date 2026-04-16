using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Services;
using KmitlNetAuth.Tray.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
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

    // Cached page instances keyed by type. Pages have constructor dependencies
    // that the default wpfui Activator-based creation cannot satisfy, so we
    // pre-build them here and expose them via a custom INavigationViewPageProvider.
    private readonly Dictionary<Type, object> _pageCache = new();

    public MainWindow(IServiceProvider services, string configPath)
    {
        // Watch OS theme so Mica/accent colors update on theme change.
        SystemThemeWatcher.Watch(this);

        InitializeComponent();

        _services = services;
        _configPath = configPath;
        _authService = services.GetRequiredService<IAuthService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();
        _updateChecker = new UpdateChecker(_logger);

        // Provide pre-instantiated pages to NavigationView so it doesn't try
        // to Activator-create pages with non-default constructors.
        RootNavigation.SetPageProviderService(new CachedPageProvider(this));

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

        // Navigate to the default page after the NavigationView is realized.
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }

    /// <summary>
    /// Creates or returns the cached page instance for the given type.
    /// Central place for wiring page constructor dependencies.
    /// </summary>
    private object CreatePage(Type pageType)
    {
        if (_pageCache.TryGetValue(pageType, out var cached))
            return cached;

        object page;
        if (pageType == typeof(DashboardPage))
            page = new DashboardPage(_services);
        else if (pageType == typeof(LogPage))
            page = new LogPage();
        else if (pageType == typeof(SettingsPage))
            page = new SettingsPage(_services, _configPath);
        else if (pageType == typeof(DebugPage))
            page = new DebugPage(_services, _configPath);
        else if (pageType == typeof(AboutPage))
            page = new AboutPage(_updateChecker);
        else
            throw new InvalidOperationException($"Unknown page type: {pageType}");

        _pageCache[pageType] = page;
        return page;
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

    /// <summary>
    /// Bridges wpfui's NavigationView page creation back to <see cref="CreatePage"/>
    /// so every page can have DI-supplied constructor arguments and be cached.
    /// </summary>
    private sealed class CachedPageProvider : INavigationViewPageProvider
    {
        private readonly MainWindow _owner;

        public CachedPageProvider(MainWindow owner) => _owner = owner;

        public object? GetPage(Type pageType) => _owner.CreatePage(pageType);
    }
}
