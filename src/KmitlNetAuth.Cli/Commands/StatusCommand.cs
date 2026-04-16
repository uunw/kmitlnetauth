using KmitlNetAuth.Core;
using Spectre.Console;

namespace KmitlNetAuth.Cli.Commands;

public static class StatusCommand
{
    public static Task ExecuteAsync(string? configPath)
    {
        var resolvedPath = ConfigPaths.Resolve(configPath);
        var config = Config.Load(resolvedPath);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]KMITL NetAuth Status[/]");

        table.AddColumn("Setting");
        table.AddColumn("Value");

        table.AddRow("Config Path", resolvedPath);
        table.AddRow("Username", string.IsNullOrEmpty(config.Username) ? "[red]Not set[/]" : config.Username);
        table.AddRow("IP Address", config.IpAddress ?? "[grey]Auto[/]");
        table.AddRow("Interval", $"{config.Interval}s");
        table.AddRow("Max Attempts", config.MaxAttempt.ToString());
        table.AddRow("Auto Login", config.AutoLogin ? "[green]Enabled[/]" : "[red]Disabled[/]");
        table.AddRow("Log Level", config.LogLevel);

        AnsiConsole.Write(table);

        return Task.CompletedTask;
    }
}
