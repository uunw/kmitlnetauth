using System.Runtime.Versioning;
using Microsoft.Win32;

namespace KmitlNetAuth.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public class WindowsAutoStartManager : IAutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "KmitlNetAuth";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.SetValue(AppName, $"\"{executablePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, false);
    }
}
