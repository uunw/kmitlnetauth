using System.CommandLine;
using KmitlNetAuth.Cli.Commands;

var configOption = new Option<string?>("--config", "-c")
{
    Description = "Path to config file",
};

var daemonOption = new Option<bool>("--daemon", "-d")
{
    Description = "Run as daemon (background mode)",
};

var rootCommand = new RootCommand("KMITL NetAuth - Auto authentication service for KMITL network");
rootCommand.Options.Add(configOption);
rootCommand.Options.Add(daemonOption);

rootCommand.SetAction(async (parseResult, ct) =>
{
    var configPath = parseResult.GetValue(configOption);
    var daemon = parseResult.GetValue(daemonOption);
    await RunCommand.ExecuteAsync(configPath, daemon);
});

var setupCommand = new Command("setup") { Description = "Interactive setup wizard" };
setupCommand.Options.Add(configOption);
setupCommand.SetAction(async (parseResult, _) =>
{
    var configPath = parseResult.GetValue(configOption);
    await SetupCommand.ExecuteAsync(configPath);
});

var statusCommand = new Command("status") { Description = "Show current configuration and status" };
statusCommand.Options.Add(configOption);
statusCommand.SetAction(async (parseResult, _) =>
{
    var configPath = parseResult.GetValue(configOption);
    await StatusCommand.ExecuteAsync(configPath);
});

var configCommand = new Command("config") { Description = "Show or open config file" };
configCommand.Options.Add(configOption);
configCommand.SetAction(async (parseResult, _) =>
{
    var configPath = parseResult.GetValue(configOption);
    await ConfigCommand.ExecuteAsync(configPath);
});

rootCommand.Subcommands.Add(setupCommand);
rootCommand.Subcommands.Add(statusCommand);
rootCommand.Subcommands.Add(configCommand);

var result = rootCommand.Parse(args);
return await result.InvokeAsync();
