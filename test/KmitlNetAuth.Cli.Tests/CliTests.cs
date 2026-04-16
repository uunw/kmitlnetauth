using System.Diagnostics;

namespace KmitlNetAuth.Cli.Tests;

public class CliTests
{
    private static readonly string CliProjectPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "KmitlNetAuth.Cli", "KmitlNetAuth.Cli.csproj"));

    private static async Task<(string StdOut, string StdErr, int ExitCode)> RunCliAsync(string args, int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {CliProjectPath} -- {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        using var cts = new CancellationTokenSource(timeoutMs);

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        return (await stdOutTask, await stdErrTask, process.ExitCode);
    }

    [Fact]
    public async Task Help_ShowsUsageInfo()
    {
        var (stdout, stderr, exitCode) = await RunCliAsync("--help");
        var output = stdout + stderr;

        Assert.Contains("KMITL NetAuth", output);
        Assert.Contains("setup", output);
        Assert.Contains("status", output);
        Assert.Contains("config", output);
    }

    [Fact]
    public async Task Version_ShowsVersion()
    {
        var (stdout, stderr, exitCode) = await RunCliAsync("--version");
        var output = (stdout + stderr).Trim();

        Assert.False(string.IsNullOrWhiteSpace(output), "Version output should not be empty");
    }

    [Fact]
    public async Task Status_ShowsConfigTable()
    {
        var (stdout, stderr, exitCode) = await RunCliAsync("status");
        var output = stdout + stderr;

        Assert.Contains("Config Path", output);
        Assert.Contains("Username", output);
    }

    [Fact]
    public async Task Setup_Help_ShowsSetupInfo()
    {
        var (stdout, stderr, exitCode) = await RunCliAsync("setup --help");
        var output = stdout + stderr;

        Assert.Contains("setup", output.ToLowerInvariant());
    }

    [Fact]
    public async Task UnknownCommand_ShowsError()
    {
        var (stdout, stderr, exitCode) = await RunCliAsync("badcommand");

        Assert.NotEqual(0, exitCode);
    }
}
