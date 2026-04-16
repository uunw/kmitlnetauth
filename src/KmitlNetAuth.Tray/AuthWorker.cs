using KmitlNetAuth.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Tray;

public sealed class AuthWorker : BackgroundService
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthWorker> _logger;

    public AuthWorker(IAuthService authService, ILogger<AuthWorker> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KMITL NetAuth tray worker started.");

        try
        {
            await _authService.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("KMITL NetAuth tray worker stopping.");
        }
    }
}
