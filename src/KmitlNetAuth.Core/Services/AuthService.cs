using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Core.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuthClient _authClient;
    private readonly Config _config;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AuthService> _logger;

    public AuthStatus CurrentStatus { get; private set; } = AuthStatus.Offline;
    public event EventHandler<AuthStatusChangedEventArgs>? StatusChanged;

    public AuthService(
        AuthClient authClient,
        Config config,
        INotificationService notificationService,
        ILogger<AuthService> logger)
    {
        _authClient = authClient;
        _config = config;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var loginAttempts = 0u;
        var maxAttempts = _config.MaxAttempt;
        var wasConnected = true;

        _logger.LogInformation("Auth service started. Username: {Username}, Interval: {Interval}s",
            _config.Username, _config.Interval);

        while (!ct.IsCancellationRequested)
        {
            if (!_config.AutoLogin)
            {
                SetStatus(AuthStatus.Paused);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                continue;
            }

            SetStatus(AuthStatus.Connecting);
            var hasInternet = await _authClient.CheckInternetAsync(ct);

            if (hasInternet)
            {
                if (!wasConnected)
                {
                    _logger.LogInformation("Internet connection restored.");
                    _notificationService.Show("Connected", "Internet connection is active.");
                    wasConnected = true;
                }

                loginAttempts = 0;
                SetStatus(AuthStatus.Online);

                var heartbeatOk = await _authClient.HeartbeatAsync(ct);
                if (!heartbeatOk)
                {
                    _logger.LogInformation("Heartbeat failed, attempting login...");
                    await _authClient.LoginAsync(ct);
                }
            }
            else
            {
                if (wasConnected)
                {
                    _logger.LogWarning("Internet connection lost.");
                    _notificationService.Show("Disconnected", "Internet connection lost. Attempting to reconnect...");
                    wasConnected = false;
                }

                SetStatus(AuthStatus.Offline);
                _logger.LogWarning("No internet connection. Attempting login...");

                if (loginAttempts < maxAttempts)
                {
                    await _authClient.LoginAsync(ct);
                    loginAttempts++;
                }
                else
                {
                    _logger.LogError("Max login attempts reached. Waiting...");
                    await Task.Delay(TimeSpan.FromSeconds(60), ct);
                    loginAttempts = 0;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.Interval), ct);
        }
    }

    private void SetStatus(AuthStatus newStatus)
    {
        if (CurrentStatus == newStatus)
            return;

        var old = CurrentStatus;
        CurrentStatus = newStatus;
        StatusChanged?.Invoke(this, new AuthStatusChangedEventArgs { OldStatus = old, NewStatus = newStatus });
    }
}
