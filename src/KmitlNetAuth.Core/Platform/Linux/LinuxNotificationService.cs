using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Core.Platform.Linux;

public class LinuxNotificationService : INotificationService
{
    private readonly ILogger<LinuxNotificationService> _logger;

    public LinuxNotificationService(ILogger<LinuxNotificationService> logger)
    {
        _logger = logger;
    }

    public void Show(string title, string body)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "notify-send",
                ArgumentList = { "--app-name=KMITL NetAuth", title, body },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
        }
        catch (Exception e)
        {
            _logger.LogWarning("Failed to show notification: {Error}", e.Message);
        }
    }
}
