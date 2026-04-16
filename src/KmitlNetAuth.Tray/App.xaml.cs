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

        var logDir = ConfigPaths.GetLogDirectory();
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(LogBufferSink.Instance)
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

        var needsSetup = string.IsNullOrEmpty(config.Username);

        var mainWindow = new MainWindow(_host.Services, configPath, navigateToSettings: needsSetup);
        MainWindow = mainWindow;

        // Always show window on first-time setup; otherwise respect StartMinimized
        if (needsSetup || !config.StartMinimized)
        {
            mainWindow.Show();
        }
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
