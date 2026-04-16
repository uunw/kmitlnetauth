using KmitlNetAuth.Core;

namespace KmitlNetAuth.Cli.Commands;

public static class SetupCommand
{
    public static Task ExecuteAsync(string? configPath)
    {
        var resolvedPath = ConfigPaths.Resolve(configPath);

        KmitlNetAuth.Core.Platform.ICredentialStore store;
        if (OperatingSystem.IsWindows())
            store = CreateWindowsStore();
        else
            store = new KmitlNetAuth.Core.Platform.Linux.FileCredentialStore();

        SetupWizard.Run(resolvedPath, store);
        return Task.CompletedTask;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static KmitlNetAuth.Core.Platform.ICredentialStore CreateWindowsStore()
    {
        return new KmitlNetAuth.Core.Platform.Windows.DpapiCredentialStore();
    }
}
