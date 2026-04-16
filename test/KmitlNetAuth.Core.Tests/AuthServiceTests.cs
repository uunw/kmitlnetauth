using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using KmitlNetAuth.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace KmitlNetAuth.Core.Tests;

public sealed class AuthServiceTests
{
    private readonly AuthClient _authClient;
    private readonly Config _config;
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();

    public AuthServiceTests()
    {
        // Build a real AuthClient backed by mocked dependencies so NSubstitute
        // can intercept the virtual-less methods via a wrapper approach.
        // AuthClient is sealed and its methods are not virtual, so we cannot
        // substitute it directly.  Instead we create a real instance whose
        // HttpClient is controlled by a MockHttpHandler.
        _config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 1,        // 1-second loop for fast tests
            BackoffInterval = 1,
            MaxAttempt = 2,
        };

        var handler = new MockHttpHandler();
        handler.SetResponse(System.Net.HttpStatusCode.OK, "success");
        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var credStore = Substitute.For<ICredentialStore>();

        _authClient = new AuthClient(
            httpClient, _config, networkInfo, credStore,
            _notificationService, NullLogger<AuthClient>.Instance);
    }

    private AuthService CreateService(Config? config = null)
    {
        return new AuthService(
            _authClient,
            config ?? _config,
            _notificationService,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RunAsync_WhenAutoLoginFalse_DoesNotLogin()
    {
        var config = new Config
        {
            AutoLogin = false,
            Interval = 1,
        };
        // Need a separate AuthClient for this config since AuthService reads config.AutoLogin
        var handler = new MockHttpHandler();
        handler.SetResponse(System.Net.HttpStatusCode.OK, "success");
        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("000000000000");
        var service = new AuthService(
            new AuthClient(httpClient, config, networkInfo, null,
                _notificationService, NullLogger<AuthClient>.Instance),
            config,
            _notificationService,
            NullLogger<AuthService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try { await service.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        Assert.Equal(AuthStatus.Paused, service.CurrentStatus);
        // The handler should not have received any requests (no login/heartbeat/check)
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task RunAsync_WhenOnline_CallsHeartbeat()
    {
        // Internet check returns "success", heartbeat returns 200
        var handler = new MockHttpHandler();
        handler.SetResponse(System.Net.HttpStatusCode.OK, "success");
        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 1,
        };
        var client = new AuthClient(httpClient, config, networkInfo, null,
            _notificationService, NullLogger<AuthClient>.Instance);
        var service = new AuthService(client, config, _notificationService,
            NullLogger<AuthService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await service.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // After checking internet (GET) and heartbeat (POST), status should be Online
        Assert.Equal(AuthStatus.Online, service.CurrentStatus);
        // At least one POST request was made (heartbeat)
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task RunAsync_WhenHeartbeatFails_AttemptsLogin()
    {
        // We need the internet check to succeed but heartbeat to fail.
        // Since both go through the same HttpClient, we use a stateful handler.
        var callCount = 0;
        var handler = new StatefulHttpHandler(request =>
        {
            callCount++;
            // First call: CheckInternetAsync (GET) -> success
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    { Content = new StringContent("success") };

            // POST calls: heartbeat first (fails), then login
            // HeartbeatUrl contains "network-api", PortalUrl contains "portalauth"
            var url = request.RequestUri?.ToString() ?? "";
            if (url.Contains("network-api"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

            // Login attempt
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("Login OK") };
        });

        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 1,
        };
        var client = new AuthClient(httpClient, config, networkInfo, null,
            _notificationService, NullLogger<AuthClient>.Instance);
        var service = new AuthService(client, config, _notificationService,
            NullLogger<AuthService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await service.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Should have made at least 3 requests: check internet, heartbeat, login
        Assert.True(callCount >= 3);
    }

    [Fact]
    public async Task RunAsync_WhenOffline_AttemptsLogin()
    {
        // Internet check fails -> offline -> login attempt
        var loginAttempted = false;
        var handler = new StatefulHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    { Content = new StringContent("not connected") };

            // Any POST is a login attempt
            loginAttempted = true;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("Login OK") };
        });

        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 1,
            MaxAttempt = 5,
        };
        var client = new AuthClient(httpClient, config, networkInfo, null,
            _notificationService, NullLogger<AuthClient>.Instance);
        var service = new AuthService(client, config, _notificationService,
            NullLogger<AuthService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await service.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        Assert.True(loginAttempted);
    }

    [Fact]
    public async Task RunAsync_MaxAttemptsReached_Backoff()
    {
        var loginCount = 0;
        var handler = new StatefulHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    { Content = new StringContent("offline") };

            loginCount++;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("Login OK") };
        });

        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 1,
            MaxAttempt = 2,
            BackoffInterval = 1,
        };
        var client = new AuthClient(httpClient, config, networkInfo, null,
            _notificationService, NullLogger<AuthClient>.Instance);
        var service = new AuthService(client, config, _notificationService,
            NullLogger<AuthService>.Instance);

        // Run long enough to hit max attempts and backoff
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try { await service.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Should have made exactly MaxAttempt login calls before backing off,
        // then reset and possibly made more. At minimum, MaxAttempt logins happened.
        Assert.True(loginCount >= (int)config.MaxAttempt);
    }

    [Fact]
    public async Task StatusChanged_FiredOnTransition()
    {
        var transitions = new List<(AuthStatus Old, AuthStatus New)>();

        var handler = new MockHttpHandler();
        handler.SetResponse(System.Net.HttpStatusCode.OK, "success");
        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 1,
        };
        var client = new AuthClient(httpClient, config, networkInfo, null,
            _notificationService, NullLogger<AuthClient>.Instance);
        var service = new AuthService(client, config, _notificationService,
            NullLogger<AuthService>.Instance);

        service.StatusChanged += (_, e) =>
            transitions.Add((e.OldStatus, e.NewStatus));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await service.RunAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Should have at least one transition (Offline -> Connecting)
        Assert.NotEmpty(transitions);
        Assert.Equal(AuthStatus.Connecting, transitions[0].New);
    }

    [Fact]
    public async Task RunAsync_CancellationToken_StopsGracefully()
    {
        var handler = new MockHttpHandler();
        handler.SetResponse(System.Net.HttpStatusCode.OK, "success");
        var httpClient = new HttpClient(handler);
        var networkInfo = Substitute.For<INetworkInfo>();
        networkInfo.GetMacAddress().Returns("aabbccddeeff");
        var config = new Config
        {
            Username = "testuser",
            Password = "testpass",
            AutoLogin = true,
            Interval = 60, // long interval
        };
        var client = new AuthClient(httpClient, config, networkInfo, null,
            _notificationService, NullLogger<AuthClient>.Instance);
        var service = new AuthService(client, config, _notificationService,
            NullLogger<AuthService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Should throw OperationCanceledException (or derived TaskCanceledException) and not hang
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RunAsync(cts.Token));
    }
}

/// <summary>
/// HttpMessageHandler that delegates to a user-supplied function for flexible per-request responses.
/// </summary>
public sealed class StatefulHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public StatefulHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
