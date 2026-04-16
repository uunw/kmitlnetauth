namespace KmitlNetAuth.Core.Platform.Linux;

public class LinuxAutoStartManager : IAutoStartManager
{
    private const string AppName = "kmitlnetauth";
    private static string DesktopFilePath => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "autostart",
        $"{AppName}.desktop");

    public bool IsEnabled => File.Exists(DesktopFilePath);

    public void Enable(string executablePath)
    {
        var dir = Path.GetDirectoryName(DesktopFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var content = $"""
            [Desktop Entry]
            Type=Application
            Name=KMITL NetAuth
            Exec={executablePath} -d
            Hidden=false
            NoDisplay=false
            X-GNOME-Autostart-enabled=true
            Comment=Auto-authentication service for KMITL network
            """;

        File.WriteAllText(DesktopFilePath, content);
    }

    public void Disable()
    {
        if (File.Exists(DesktopFilePath))
            File.Delete(DesktopFilePath);
    }
}
