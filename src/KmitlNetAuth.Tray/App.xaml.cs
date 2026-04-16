using System.IO;
using System.Runtime.Versioning;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.DependencyInjection;
using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var configPath = ConfigPaths.Resolve();
        var config = Config.Load(configPath);

        // If no username, show settings window for first-time setup
        if (string.IsNullOrEmpty(config.Username))
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var setupWindow = new SettingsWindow(config, configPath, credentialStore: null);
            var result = setupWindow.ShowDialog();
            if (result != true)
            {
                Shutdown();
                return;
            }
        }

        // Warn if using DHCP and no static IP is configured
        if (string.IsNullOrEmpty(config.IpAddress))
        {
            var (isDhcp, currentIp) = DhcpDetector.GetNetworkStatus();
            if (isDhcp && !string.IsNullOrEmpty(currentIp))
            {
                var result = System.Windows.MessageBox.Show(
                    $"Your network interface is using DHCP.\n" +
                    $"Current IP: {currentIp}\n\n" +
                    $"DHCP addresses may change, which could break auto-authentication.\n" +
                    $"Would you like to save {currentIp} as your static IP in the config?\n\n" +
                    $"Click 'Yes' to use {currentIp} as static IP.\n" +
                    $"Click 'No' to continue with DHCP (auto-detect).",
                    "KMITL NetAuth - Network Configuration",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    config.IpAddress = currentIp;
                    config.Save(configPath);
                }
            }
        }

        var logDir = ConfigPaths.GetLogDirectory();
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "tray-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSerilog();
        builder.Services.AddKmitlNetAuth(config);
        builder.Services.AddHostedService<AuthWorker>();

        _host = builder.Build();
        _ = _host.StartAsync();

        var mainWindow = new MainWindow(_host.Services, configPath);
        MainWindow = mainWindow;
        // MainWindow stays hidden; it manages the tray icon
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_host != null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
