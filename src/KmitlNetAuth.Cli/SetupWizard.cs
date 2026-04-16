using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using Spectre.Console;

namespace KmitlNetAuth.Cli;

public static class SetupWizard
{
    public static Config Run(string configPath, ICredentialStore? credentialStore)
    {
        AnsiConsole.Write(new Rule("[bold blue]KMITL NetAuth Setup[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var existingConfig = new Config();
        if (File.Exists(configPath))
        {
            existingConfig = Config.Load(configPath);
            AnsiConsole.MarkupLine("[yellow]Existing config found. Current values shown as defaults.[/]");
            AnsiConsole.WriteLine();
        }

        var username = AnsiConsole.Prompt(
            new TextPrompt<string>("Student ID:")
                .DefaultValue(existingConfig.Username)
                .AllowEmpty());

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Password:")
                .Secret()
                .AllowEmpty());

        var ipAddress = AnsiConsole.Prompt(
            new TextPrompt<string>("IP Address [grey](optional, press Enter to skip)[/]:")
                .DefaultValue(existingConfig.IpAddress ?? "")
                .AllowEmpty());

        var interval = AnsiConsole.Prompt(
            new TextPrompt<ulong>("Heartbeat Interval (seconds):")
                .DefaultValue(existingConfig.Interval));

        var autoLogin = AnsiConsole.Confirm("Enable auto-login?", existingConfig.AutoLogin);

        var config = new Config
        {
            Username = username,
            Password = string.IsNullOrEmpty(password) ? null : password,
            IpAddress = string.IsNullOrEmpty(ipAddress) ? null : ipAddress,
            Interval = interval,
            MaxAttempt = existingConfig.MaxAttempt,
            AutoLogin = autoLogin,
            LogLevel = existingConfig.LogLevel,
        };

        config.Save(configPath, credentialStore);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Configuration saved to {configPath}[/]");

        if (!string.IsNullOrEmpty(password) && credentialStore != null)
        {
            try
            {
                credentialStore.SetPasswordAsync(username, password).GetAwaiter().GetResult();
                AnsiConsole.MarkupLine("[green]Password stored securely.[/]");
            }
            catch
            {
                AnsiConsole.MarkupLine("[yellow]Password saved to config file (credential store unavailable).[/]");
            }
        }

        return config;
    }
}
