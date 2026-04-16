using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace KmitlNetAuth.Tray;

/// <summary>
/// Global last-resort exception handler. Any unhandled exception on the UI
/// thread, thread-pool, or AppDomain is caught here, logged to a
/// <c>crash-YYYYMMDD-HHMMSS.log</c> file in the configured log directory, and
/// surfaced to the user with Reopen / View Log / Quit options.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public static class CrashHandler
{
    private static string _logDir = string.Empty;
    private static bool _registered;
    private static bool _handling;
    private static readonly object _sync = new();

    /// <summary>
    /// Installs global exception handlers. Safe to call multiple times — subsequent
    /// calls are no-ops.
    /// </summary>
    /// <param name="logDir">Directory where <c>crash-*.log</c> files are written.</param>
    public static void Register(string logDir)
    {
        if (_registered)
            return;
        _registered = true;

        _logDir = logDir;

        // AppDomain covers non-UI threads and anything the CLR considers
        // terminal. We can't prevent termination here, but we can log and notify.
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;

        // Swallow unobserved task exceptions so the finalizer doesn't tear the
        // process down — we still surface them via the dialog.
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // WPF Dispatcher exceptions. Application.Current may not exist yet at
        // registration time (e.g. Register called before App starts); if so,
        // defer hookup until Application.Current becomes available.
        var app = Application.Current;
        if (app != null)
        {
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }
        else
        {
            // Poll briefly on a background task to attach the handler as soon
            // as Application.Current is available. This avoids missing the
            // very first dispatcher exceptions during app startup.
            _ = Task.Run(async () =>
            {
                for (var i = 0; i < 200; i++)
                {
                    var current = Application.Current;
                    if (current != null)
                    {
                        current.Dispatcher.Invoke(() =>
                        {
                            current.DispatcherUnhandledException += OnDispatcherUnhandledException;
                        });
                        return;
                    }
                    await Task.Delay(50).ConfigureAwait(false);
                }
            });
        }
    }

    private static void OnAppDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Handle(ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Mark observed so the process isn't torn down for this exception.
        e.SetObserved();
        Handle(e.Exception);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Prevent WPF's default "application has stopped working" popup by
        // signalling that we've handled the exception ourselves.
        e.Handled = true;
        Handle(e.Exception);
    }

    private static void Handle(Exception ex)
    {
        // Guard against the handler itself throwing (which would recurse).
        lock (_sync)
        {
            if (_handling)
                return;
            _handling = true;
        }

        string crashLogPath;
        try
        {
            Log.Fatal(ex, "Unhandled exception");
        }
        catch
        {
            // If Serilog itself is unhealthy we still want to write the file.
        }

        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
            // ignore
        }

        try
        {
            crashLogPath = WriteCrashFile(ex);
        }
        catch
        {
            // Fall back to a temp-path crash log so the user can still find it.
            crashLogPath = Path.Combine(Path.GetTempPath(), $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            try { File.WriteAllText(crashLogPath, FormatException(ex)); } catch { /* last resort */ }
        }

        ShowDialogAndExit(ex, crashLogPath);
    }

    private static string WriteCrashFile(Exception ex)
    {
        Directory.CreateDirectory(_logDir);
        var path = Path.Combine(_logDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(path, FormatException(ex));
        return path;
    }

    private static string FormatException(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Process:   {Environment.ProcessPath}");
        sb.AppendLine($"OS:        {Environment.OSVersion}");
        sb.AppendLine($"CLR:       {Environment.Version}");
        sb.AppendLine();

        var current = ex;
        var depth = 0;
        while (current != null)
        {
            sb.AppendLine(depth == 0 ? "=== Exception ===" : $"=== Inner exception #{depth} ===");
            sb.AppendLine($"Type:    {current.GetType().FullName}");
            sb.AppendLine($"Message: {current.Message}");
            if (!string.IsNullOrEmpty(current.Source))
                sb.AppendLine($"Source:  {current.Source}");
            sb.AppendLine("Stack trace:");
            sb.AppendLine(current.StackTrace ?? "(no stack trace)");
            sb.AppendLine();
            current = current.InnerException;
            depth++;
        }

        return sb.ToString();
    }

    private static void ShowDialogAndExit(Exception ex, string crashLogPath)
    {
        var message =
            $"KMITL NetAuth has encountered an unexpected error and cannot continue.\n\n" +
            $"{ex.GetType().Name}: {ex.Message}\n\n" +
            $"A crash log has been written to:\n{crashLogPath}\n\n" +
            $"Yes\t-  Reopen the application\n" +
            $"No\t-  Open the crash log\n" +
            $"Cancel\t-  Quit";

        // Use the plain Win32-style MessageBox so we don't depend on a live WPF
        // dispatcher (which may be the thing that just died).
        MessageBoxResult result;
        try
        {
            result = MessageBox.Show(
                message,
                "KMITL NetAuth - Crash",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Error,
                MessageBoxResult.Cancel);
        }
        catch
        {
            // If even MessageBox can't render, just exit quietly.
            Environment.Exit(1);
            return;
        }

        try
        {
            switch (result)
            {
                case MessageBoxResult.Yes:
                    var exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe))
                    {
                        Process.Start(new ProcessStartInfo(exe)
                        {
                            UseShellExecute = false,
                        });
                    }
                    break;
                case MessageBoxResult.No:
                    // UseShellExecute=true lets Windows pick the default editor
                    // (Notepad, etc.) for .log files.
                    Process.Start(new ProcessStartInfo(crashLogPath)
                    {
                        UseShellExecute = true,
                    });
                    break;
                case MessageBoxResult.Cancel:
                default:
                    // fall through to Environment.Exit below
                    break;
            }
        }
        catch
        {
            // If launching Reopen / View Log fails we still want to exit.
        }

        Environment.Exit(1);
    }
}
