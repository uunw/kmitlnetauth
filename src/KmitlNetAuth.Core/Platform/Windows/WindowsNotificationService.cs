using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public class WindowsNotificationService : INotificationService
{
    private readonly ILogger<WindowsNotificationService> _logger;

    public WindowsNotificationService(ILogger<WindowsNotificationService> logger)
    {
        _logger = logger;
    }

    public void Show(string title, string body)
    {
        // Windows notifications are handled by the Tray app's NotifyIcon.
        // When running as a headless CLI/service, we just log the notification.
        _logger.LogInformation("[Notification] {Title}: {Body}", title, body);
    }
}
