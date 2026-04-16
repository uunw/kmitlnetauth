using KmitlNetAuth.Core.Exceptions;
using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.Logging;

namespace KmitlNetAuth.Core;

public sealed class AuthClient
{
    private const string LoginUrl = "https://portal.kmitl.ac.th:19008/portalauth/login";
    private const string HeartbeatUrl = "https://nani.csc.kmitl.ac.th/network-api/data/";
    private const string CheckUrl = "http://detectportal.firefox.com/success.txt";
    private const string Acip = "10.252.13.10";

    private readonly HttpClient _httpClient;
    private readonly Config _config;
    private readonly ICredentialStore? _credentialStore;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AuthClient> _logger;
    private readonly string _macAddress;

    public AuthClient(
        HttpClient httpClient,
        Config config,
        INetworkInfo networkInfo,
        ICredentialStore? credentialStore,
        INotificationService notificationService,
        ILogger<AuthClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _credentialStore = credentialStore;
        _notificationService = notificationService;
        _logger = logger;
        _macAddress = networkInfo.GetMacAddress();
    }

    public async Task<bool> LoginAsync(CancellationToken ct = default)
    {
        var username = _config.Username;
        var password = _config.GetPassword(_credentialStore);
        var ipAddress = _config.IpAddress ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("Username or password empty. Skipping login.");
            return false;
        }

        _logger.LogInformation("Logging in with username '{Username}'...", username);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = username,
            ["userPass"] = password,
            ["uaddress"] = ipAddress,
            ["umac"] = _macAddress,
            ["agreed"] = "1",
            ["acip"] = Acip,
            ["authType"] = "1",
        });

        try
        {
            var response = await _httpClient.PostAsync(LoginUrl, form, ct);

            if (response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                _logger.LogDebug("Login response: {Response}", text);
                _logger.LogInformation("Login request sent successfully.");
                _notificationService.Show("Login Successful", $"Logged in as {username}");
                return true;
            }

            _logger.LogError("Login failed with status: {Status}", response.StatusCode);
            _notificationService.Show("Login Failed", $"Status: {response.StatusCode}");
            return false;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Login connection error");
            return false;
        }
    }

    public async Task<bool> HeartbeatAsync(CancellationToken ct = default)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _config.Username,
            ["os"] = "Chrome v116.0.5845.141 on Windows 10 64-bit",
            ["speed"] = "1.29",
            ["newauth"] = "1",
        });

        try
        {
            var response = await _httpClient.PostAsync(HeartbeatUrl, form, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Heartbeat OK");
                return true;
            }

            _logger.LogWarning("Heartbeat failed with status: {Status}", response.StatusCode);
            return false;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning("Heartbeat connection error: {Error}", e.Message);
            return false;
        }
    }

    public async Task<bool> CheckInternetAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(CheckUrl, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            return text.Trim() == "success";
        }
        catch
        {
            return false;
        }
    }
}
