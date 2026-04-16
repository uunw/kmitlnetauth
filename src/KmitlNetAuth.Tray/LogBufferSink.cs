using System.Runtime.Versioning;
using Serilog.Core;
using Serilog.Events;

namespace KmitlNetAuth.Tray;

[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class LogBufferSink : ILogEventSink
{
    public static LogBufferSink Instance { get; } = new();

    private readonly List<string> _buffer = new();
    private readonly object _lock = new();

    public event Action<string>? LogReceived;

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        var line = $"[{logEvent.Timestamp:HH:mm:ss}] [{logEvent.Level}] {message}";
        if (logEvent.Exception != null)
            line += Environment.NewLine + logEvent.Exception;

        lock (_lock)
        {
            _buffer.Add(line);
            if (_buffer.Count > 10000)
                _buffer.RemoveAt(0);
        }

        LogReceived?.Invoke(line);
    }

    public IReadOnlyList<string> GetAll()
    {
        lock (_lock) return _buffer.ToList();
    }

    public void Clear()
    {
        lock (_lock) _buffer.Clear();
    }
}
