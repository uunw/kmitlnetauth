using System.Runtime.Versioning;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.DependencyInjection;
using KmitlNetAuth.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var configPath = ConfigPaths.Resolve();
        var config = Config.Load(configPath);

        // If no username, show settings form for first-time setup
        if (string.IsNullOrEmpty(config.Username))
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var setupForm = new SettingsForm(config, configPath, credentialStore: null);
            if (setupForm.ShowDialog() != DialogResult.OK)
                return; // User cancelled first-time setup
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

        var host = builder.Build();
        _ = host.StartAsync();

        Application.Run(new TrayApplicationContext(host.Services, configPath));

        host.StopAsync().GetAwaiter().GetResult();
        Log.CloseAndFlush();
    }
}
