using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows")]
internal static class NativeConsole
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private static bool _allocated;

    public static void Show()
    {
        var hwnd = GetConsoleWindow();
        if (hwnd == IntPtr.Zero)
        {
            AllocConsole();
            _allocated = true;
            // Redirect stdout/stderr to the new console so Serilog output appears
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        else
        {
            ShowWindow(hwnd, SW_SHOW);
        }
    }

    public static void Hide()
    {
        var hwnd = GetConsoleWindow();
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_HIDE);
    }

    public static bool IsVisible
    {
        get
        {
            var hwnd = GetConsoleWindow();
            return hwnd != IntPtr.Zero && _allocated;
        }
    }
}
