using KmitlNetAuth.Core;
using KmitlNetAuth.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KmitlNetAuth.Cli.Commands;

public static class RunCommand
{
    public static async Task ExecuteAsync(string? configPath, bool daemon)
    {
        var resolvedPath = ConfigPaths.Resolve(configPath);
        var config = Config.Load(resolvedPath);

        if (string.IsNullOrEmpty(config.Username))
        {
            if (daemon || !Environment.UserInteractive)
            {
                Console.Error.WriteLine("Error: Username not set in config. Run 'kmitlnetauth setup' first.");
                Environment.Exit(1);
            }

            // Interactive - run setup wizard
            var tempStore = CreateCredentialStore();
            config = SetupWizard.Run(resolvedPath, tempStore);
        }

        var logDir = ConfigPaths.GetLogDirectory();
        Directory.CreateDirectory(logDir);

        var logLevel = ParseLogLevel(config.LogLevel);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "kmitlnetauth-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSerilog();
            builder.Services.AddKmitlNetAuth(config);
            builder.Services.AddHostedService<AuthWorker>();

            if (OperatingSystem.IsLinux())
                builder.Services.AddSystemd();
            if (OperatingSystem.IsWindows())
                builder.Services.AddWindowsService();

            var host = builder.Build();

            Log.Information("Starting KMITL NetAuth Service ({Mode})", daemon ? "Daemon" : "Foreground");
            Log.Information("Using config file: {ConfigPath}", resolvedPath);

            await host.RunAsync();
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static KmitlNetAuth.Core.Platform.ICredentialStore CreateCredentialStore()
    {
        if (OperatingSystem.IsWindows())
            return CreateWindowsStore();

        return new KmitlNetAuth.Core.Platform.Linux.FileCredentialStore();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static KmitlNetAuth.Core.Platform.ICredentialStore CreateWindowsStore()
    {
        return new KmitlNetAuth.Core.Platform.Windows.DpapiCredentialStore();
    }

    private static Serilog.Events.LogEventLevel ParseLogLevel(string level) => level.ToLowerInvariant() switch
    {
        "verbose" or "trace" => Serilog.Events.LogEventLevel.Verbose,
        "debug" => Serilog.Events.LogEventLevel.Debug,
        "information" or "info" => Serilog.Events.LogEventLevel.Information,
        "warning" or "warn" => Serilog.Events.LogEventLevel.Warning,
        "error" => Serilog.Events.LogEventLevel.Error,
        "fatal" => Serilog.Events.LogEventLevel.Fatal,
        _ => Serilog.Events.LogEventLevel.Information,
    };
}
