using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

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
        Dispatcher.Invoke(() =>
        {
            if (PassesFilter(line))
            {
                LogOutput.AppendText(line + Environment.NewLine);
                LogOutput.ScrollToEnd();
            }
        });
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
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
        LogOutput.Clear();
        var allLines = LogBufferSink.Instance.GetAll();
        var filtered = allLines.Where(PassesFilter);
        LogOutput.Text = string.Join(Environment.NewLine, filtered);
        LogOutput.ScrollToEnd();
    }

    private bool PassesFilter(string line)
    {
        if (_currentFilter == "All")
            return true;

        // Match the log level token in the formatted line: [HH:mm:ss] [Level] message
        return line.Contains($"[{_currentFilter}]", StringComparison.OrdinalIgnoreCase);
    }
}
