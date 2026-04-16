namespace KmitlNetAuth.Core.Services;

public interface IAuthService
{
    Task RunAsync(CancellationToken ct);
    AuthStatus CurrentStatus { get; }
    event EventHandler<AuthStatusChangedEventArgs>? StatusChanged;
}
