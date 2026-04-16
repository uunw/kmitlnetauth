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

        CrashHandler.Register(logDir);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSerilog();
        builder.Services.AddKmitlNetAuth(config);
        builder.Services.AddHostedService<AuthWorker>();

        _host = builder.Build();

        // First-run wizard: username is empty -> force completion BEFORE the
        // hosted AuthWorker starts, so it picks up the saved credentials.
        var wizardRan = false;
        if (string.IsNullOrWhiteSpace(config.Username))
        {
            var setup = new SetupWindow(_host.Services, configPath);
            var ok = setup.ShowDialog();
            if (ok != true)
            {
                Shutdown();
                return;
            }
            wizardRan = true;
        }

        _ = _host.StartAsync();

        var mainWindow = new MainWindow(_host.Services, configPath);
        MainWindow = mainWindow;

        // First-run: always show (user just configured and expects to see it).
        // Subsequent runs: respect StartMinimized preference.
        if (wizardRan || !config.StartMinimized)
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
