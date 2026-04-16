namespace KmitlNetAuth.Core;

public enum AuthStatus
{
    Online,
    Offline,
    Connecting,
    Paused,
}

public sealed class AuthStatusChangedEventArgs : EventArgs
{
    public AuthStatus OldStatus { get; init; }
    public AuthStatus NewStatus { get; init; }
}
