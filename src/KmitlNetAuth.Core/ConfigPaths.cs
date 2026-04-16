namespace KmitlNetAuth.Core;

public static class ConfigPaths
{
    private const string AppName = "kmitlnetauth";
    private const string ConfigFileName = "config.toml";

    public static string Resolve(string? explicitPath = null)
    {
        if (!string.IsNullOrEmpty(explicitPath))
            return explicitPath;

        if (OperatingSystem.IsLinux())
        {
            var globalPath = $"/etc/{AppName}/{ConfigFileName}";
            if (File.Exists(globalPath))
                return globalPath;

            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(xdgConfig, AppName, ConfigFileName);
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppName, ConfigFileName);
        }

        // macOS / fallback
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", AppName, ConfigFileName);
    }

    public static string GetLogDirectory()
    {
        if (OperatingSystem.IsLinux())
        {
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(xdgData, AppName, "logs");
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, AppName, "logs");
        }

        // macOS / fallback
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", AppName, "logs");
    }

    public static string GetCredentialPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppName, "credentials.dat");
        }

        // Linux / macOS
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(xdgConfig, AppName, ".credentials");
    }
}
