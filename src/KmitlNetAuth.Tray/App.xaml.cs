using System.IO;
using System.Runtime.Versioning;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.DependencyInjection;
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
