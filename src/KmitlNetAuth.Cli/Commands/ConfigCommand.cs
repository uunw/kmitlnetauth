using System.Diagnostics;
using KmitlNetAuth.Core;
using Spectre.Console;

namespace KmitlNetAuth.Cli.Commands;

public static class ConfigCommand
{
    public static Task ExecuteAsync(string? configPath)
    {
        var resolvedPath = ConfigPaths.Resolve(configPath);

        AnsiConsole.MarkupLine($"Config file: [bold]{resolvedPath}[/]");

        if (!File.Exists(resolvedPath))
        {
            AnsiConsole.MarkupLine("[yellow]Config file does not exist yet. Run 'kmitlnetauth setup' to create it.[/]");
            return Task.CompletedTask;
        }

        // Try to open in default editor
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resolvedPath,
                UseShellExecute = true,
            });
        }
        catch
        {
            AnsiConsole.MarkupLine("[grey]Could not open file in editor. Edit manually at the path above.[/]");
        }

        return Task.CompletedTask;
    }
}
