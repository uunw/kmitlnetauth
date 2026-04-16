using System.Net;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace KmitlNetAuth.Core.Tests;

/// <summary>
/// Delegating handler that returns a preconfigured response or throws an exception.
/// </summary>
public sealed class MockHttpHandler : HttpMessageHandler
{
    private HttpResponseMessage? _response;
    private Exception? _exception;
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestContent { get; private set; }

    public void SetResponse(HttpStatusCode status, string content = "")
    {
        _response = new HttpResponseMessage(status) { Content = new StringContent(content) };
        _exception = null;
    }

    public void SetException(Exception ex)
    {
        _exception = ex;
        _response = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content != null)
            LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);

        if (_exception != null)
            throw _exception;

        return _response ?? new HttpResponseMessage(HttpStatusCode.OK);
    }
}

public sealed class AuthClientTests
{
    private readonly MockHttpHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly INetworkInfo _networkInfo = Substitute.For<INetworkInfo>();
    private readonly ICredentialStore _credentialStore = Substitute.For<ICredentialStore>();

    public AuthClientTests()
    {
        _httpClient = new HttpClient(_handler);
        _networkInfo.GetMacAddress().Returns("aabbccddeeff");
    }

    private AuthClient CreateClient(Config? config = null)
    {
        config ??= new Config
        {
            Username = "testuser",
            Password = "testpass",
        };
        return new AuthClient(
            _httpClient,
            config,
            _networkInfo,
            _credentialStore,
            _notificationService,
            NullLogger<AuthClient>.Instance);
    }

    // --- LoginAsync ---

    [Fact]
    public async Task LoginAsync_Success_ReturnsTrue()
    {
        _handler.SetResponse(HttpStatusCode.OK, "Login OK");
        var client = CreateClient();

        var result = await client.LoginAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task LoginAsync_HttpError_ReturnsFalse()
    {
        _handler.SetResponse(HttpStatusCode.InternalServerError);
        var client = CreateClient();

        var result = await client.LoginAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task LoginAsync_EmptyCredentials_ReturnsFalse()
    {
        _handler.SetResponse(HttpStatusCode.OK);
        var config = new Config { Username = "", Password = "" };
        var client = CreateClient(config);

        var result = await client.LoginAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task LoginAsync_SendsCorrectFormData()
    {
        _handler.SetResponse(HttpStatusCode.OK);
        var config = new Config
        {
            Username = "formuser",
            Password = "formpass",
            IpAddress = "10.0.0.5",
        };
        var client = CreateClient(config);

        await client.LoginAsync();

        Assert.NotNull(_handler.LastRequestContent);
        var content = _handler.LastRequestContent;
        Assert.Contains("userName=formuser", content);
        Assert.Contains("userPass=formpass", content);
        Assert.Contains("umac=aabbccddeeff", content);
        Assert.Contains("agreed=1", content);
        Assert.Contains("acip=10.252.13.10", content);
        Assert.Contains("authType=1", content);
    }

    // --- HeartbeatAsync ---

    [Fact]
    public async Task HeartbeatAsync_Success_ReturnsTrue()
    {
        _handler.SetResponse(HttpStatusCode.OK);
        var client = CreateClient();

        var result = await client.HeartbeatAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task HeartbeatAsync_Failure_ReturnsFalse()
    {
        _handler.SetResponse(HttpStatusCode.InternalServerError);
        var client = CreateClient();

        var result = await client.HeartbeatAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task HeartbeatAsync_SendsCorrectFormData()
    {
        _handler.SetResponse(HttpStatusCode.OK);
        var config = new Config
        {
            Username = "hbuser",
            HeartbeatUserAgent = "TestAgent/1.0",
        };
        var client = CreateClient(config);

        await client.HeartbeatAsync();

        Assert.NotNull(_handler.LastRequestContent);
        var content = _handler.LastRequestContent;
        Assert.Contains("username=hbuser", content);
        Assert.Contains($"os={Uri.EscapeDataString("TestAgent/1.0")}", content);
        Assert.Contains("speed=1.29", content);
        Assert.Contains("newauth=1", content);
    }

    // --- CheckInternetAsync ---

    [Fact]
    public async Task CheckInternetAsync_Success_ReturnsTrue()
    {
        _handler.SetResponse(HttpStatusCode.OK, "success");
        var client = CreateClient();

        var result = await client.CheckInternetAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task CheckInternetAsync_NotSuccess_ReturnsFalse()
    {
        _handler.SetResponse(HttpStatusCode.OK, "other");
        var client = CreateClient();

        var result = await client.CheckInternetAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task CheckInternetAsync_NetworkError_ReturnsFalse()
    {
        _handler.SetException(new HttpRequestException("Network unreachable"));
        var client = CreateClient();

        var result = await client.CheckInternetAsync();

        Assert.False(result);
    }
}
