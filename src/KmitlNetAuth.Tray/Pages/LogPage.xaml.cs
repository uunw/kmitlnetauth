using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace KmitlNetAuth.Tray.Pages;

[SupportedOSPlatform("windows10.0.17763.0")]
public partial class LogPage : Page
{
    private string _currentFilter = "All";

    public LogPage()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load all existing log entries
        RefreshLog();

        // Subscribe to new entries
        LogBufferSink.Instance.LogReceived += OnLogReceived;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LogBufferSink.Instance.LogReceived -= OnLogReceived;
    }

    private void OnLogReceived(string line)
    {
        // This event may fire on any thread; marshal onto the UI thread.
        // Wrap in try/catch so a stray UI failure never bubbles up into the
        // logging pipeline (which would trigger another log, and so on).
        try
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (LogOutput == null)
                        return;
                    if (PassesFilter(line))
                    {
                        LogOutput.AppendText(line + Environment.NewLine);
                        LogOutput.ScrollToEnd();
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "LogPage.OnLogReceived UI update failed");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "LogPage.OnLogReceived dispatch failed");
        }
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        // ComboBoxItem.IsSelected="True" in XAML causes SelectionChanged to
        // fire during InitializeComponent, before LogOutput has been assigned.
        // Guard against the null control and against any other transient issue.
        if (LogOutput == null)
            return;

        if (LevelFilter.SelectedItem is ComboBoxItem item && item.Content is string level)
        {
            _currentFilter = level;
            RefreshLog();
        }
    }

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        LogBufferSink.Instance.Clear();
        LogOutput.Clear();
    }

    private void RefreshLog()
    {
        try
        {
            if (LogOutput == null)
                return;

            LogOutput.Clear();
            var allLines = LogBufferSink.Instance.GetAll();
            var filtered = allLines.Where(PassesFilter);
            LogOutput.Text = string.Join(Environment.NewLine, filtered);
            LogOutput.ScrollToEnd();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "LogPage.RefreshLog failed");
        }
    }

    private bool PassesFilter(string line)
    {
        if (_currentFilter == "All")
            return true;

        // Match the log level token in the formatted line: [HH:mm:ss] [Level] message
        return line.Contains($"[{_currentFilter}]", StringComparison.OrdinalIgnoreCase);
    }
}
