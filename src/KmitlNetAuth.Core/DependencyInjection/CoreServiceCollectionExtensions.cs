using System.Net;
using KmitlNetAuth.Core.Platform;
using KmitlNetAuth.Core.Platform.Linux;
using KmitlNetAuth.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KmitlNetAuth.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public const string HttpClientName = "KmitlAuth";

    public static IServiceCollection AddKmitlNetAuth(this IServiceCollection services, Config config)
    {
        services.AddSingleton(config);

        services.AddHttpClient(HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(config.Timeout);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            ServerCertificateCustomValidationCallback = config.AcceptInvalidCerts
                ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                : null,
        });

        // Platform-specific services
        if (OperatingSystem.IsWindows())
        {
            RegisterWindowsServices(services);
        }
        else
        {
            services.AddSingleton<ICredentialStore, FileCredentialStore>();
            services.AddSingleton<INotificationService, LinuxNotificationService>();
            services.AddSingleton<IAutoStartManager, LinuxAutoStartManager>();
        }

        services.AddSingleton<INetworkInfo, NetworkInfo>();

        // Auth client - resolve HttpClient from named factory
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(HttpClientName);
            return new AuthClient(
                httpClient,
                sp.GetRequiredService<Config>(),
                sp.GetRequiredService<INetworkInfo>(),
                sp.GetService<ICredentialStore>(),
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AuthClient>>());
        });

        services.AddSingleton<IAuthService, AuthService>();

        return services;
    }

    // Separate method to avoid loading Windows-specific types on Linux
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterWindowsServices(IServiceCollection services)
    {
        services.AddSingleton<ICredentialStore, Platform.Windows.DpapiCredentialStore>();
        services.AddSingleton<INotificationService, Platform.Windows.WindowsNotificationService>();
        services.AddSingleton<IAutoStartManager, Platform.Windows.WindowsAutoStartManager>();
    }
}
