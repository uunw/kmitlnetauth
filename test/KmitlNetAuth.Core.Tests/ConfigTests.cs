using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace KmitlNetAuth.Core.Tests;

public sealed class ConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _envVarsToClean = [];

    public ConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"kmitl_config_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var key in _envVarsToClean)
            Environment.SetEnvironmentVariable(key, null);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string TempFile(string name = "config.toml") => Path.Combine(_tempDir, name);

    private void SetEnv(string key, string value)
    {
        _envVarsToClean.Add(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        var path = TempFile();
        File.WriteAllText(path, "");

        var config = Config.Load(path);

        Assert.Equal("", config.Username);
        Assert.Null(config.Password);
        Assert.Null(config.IpAddress);
        Assert.Equal("https://portal.kmitl.ac.th:19008/portalauth/login", config.PortalUrl);
        Assert.Equal("https://nani.csc.kmitl.ac.th/network-api/data/", config.HeartbeatUrl);
        Assert.Equal("http://detectportal.firefox.com/success.txt", config.InternetCheckUrl);
        Assert.Equal(10, config.Timeout);
        Assert.True(config.AcceptInvalidCerts);
        Assert.Equal(300UL, config.Interval);
        Assert.Equal(20U, config.MaxAttempt);
        Assert.Equal(60, config.BackoffInterval);
        Assert.True(config.AutoLogin);
        Assert.Equal("Information", config.LogLevel);
        Assert.Null(config.LogDirectory);
        Assert.Equal(30, config.LogRetentionDays);
        Assert.True(config.NotificationsEnabled);
        Assert.True(config.AutoUpdateCheck);
        Assert.Equal(24, config.UpdateCheckIntervalHours);
        Assert.True(config.StartMinimized);
    }

    [Fact]
    public void Load_ValidToml_ParsesCorrectly()
    {
        var path = TempFile();
        File.WriteAllText(path, """
            [auth]
            username = "testuser"
            password = "testpass"
            ip_address = "10.0.0.5"
            portal_url = "https://custom.portal/login"
            heartbeat_url = "https://custom.hb/data/"
            internet_check_url = "http://custom.check/ok"

            [network]
            timeout = 30
            accept_invalid_certs = false
            heartbeat_user_agent = "CustomAgent/1.0"

            [service]
            interval = 600
            max_attempt = 10
            backoff_interval = 120
            auto_login = false

            [logging]
            level = "Debug"
            directory = "/var/log/kmitl"
            retention_days = 7

            [notifications]
            enabled = false

            [update]
            auto_check = false
            check_interval_hours = 48

            [tray]
            start_minimized = false
            """);

        var config = Config.Load(path);

        Assert.Equal("testuser", config.Username);
        Assert.Equal("testpass", config.Password);
        Assert.Equal("10.0.0.5", config.IpAddress);
        Assert.Equal("https://custom.portal/login", config.PortalUrl);
        Assert.Equal("https://custom.hb/data/", config.HeartbeatUrl);
        Assert.Equal("http://custom.check/ok", config.InternetCheckUrl);
        Assert.Equal(30, config.Timeout);
        Assert.False(config.AcceptInvalidCerts);
        Assert.Equal("CustomAgent/1.0", config.HeartbeatUserAgent);
        Assert.Equal(600UL, config.Interval);
        Assert.Equal(10U, config.MaxAttempt);
        Assert.Equal(120, config.BackoffInterval);
        Assert.False(config.AutoLogin);
        Assert.Equal("Debug", config.LogLevel);
        Assert.Equal("/var/log/kmitl", config.LogDirectory);
        Assert.Equal(7, config.LogRetentionDays);
        Assert.False(config.NotificationsEnabled);
        Assert.False(config.AutoUpdateCheck);
        Assert.Equal(48, config.UpdateCheckIntervalHours);
        Assert.False(config.StartMinimized);
    }

    [Fact]
    public void Load_PartialToml_UsesDefaults()
    {
        var path = TempFile();
        File.WriteAllText(path, """
            [auth]
            username = "partial_user"

            [service]
            interval = 120
            """);

        var config = Config.Load(path);

        Assert.Equal("partial_user", config.Username);
        Assert.Equal(120UL, config.Interval);
        // Password becomes "" when [auth] section exists but password key is absent
        // (GetString returns fallback ?? "" where fallback is null)
        Assert.Equal("", config.Password);
        Assert.Equal(10, config.Timeout);
        Assert.True(config.AcceptInvalidCerts);
        Assert.Equal(20U, config.MaxAttempt);
        Assert.True(config.AutoLogin);
        Assert.Equal("Information", config.LogLevel);
    }

    [Fact]
    public void Load_InvalidToml_ReturnsDefaults()
    {
        var path = TempFile();
        File.WriteAllText(path, "this is not {{ valid toml content ]]");

        var config = Config.Load(path);

        Assert.Equal("", config.Username);
        Assert.Null(config.Password);
        Assert.Equal(300UL, config.Interval);
        Assert.True(config.AutoLogin);
    }

    [Fact]
    public void Load_EnvironmentOverrides_TakePrecedence()
    {
        var path = TempFile();
        File.WriteAllText(path, """
            [auth]
            username = "file_user"

            [service]
            interval = 300
            """);

        SetEnv("KMITL_USERNAME", "env_user");
        SetEnv("KMITL_PASSWORD", "env_pass");
        SetEnv("KMITL_IP", "192.168.1.1");
        SetEnv("KMITL_INTERVAL", "999");
        SetEnv("KMITL_MAX_ATTEMPT", "5");
        SetEnv("KMITL_AUTO_LOGIN", "false");
        SetEnv("KMITL_LOG_LEVEL", "Warning");
        SetEnv("KMITL_TIMEOUT", "45");
        SetEnv("KMITL_BACKOFF_INTERVAL", "200");
        SetEnv("KMITL_NOTIFICATIONS", "false");

        var config = Config.Load(path);

        Assert.Equal("env_user", config.Username);
        Assert.Equal("env_pass", config.Password);
        Assert.Equal("192.168.1.1", config.IpAddress);
        Assert.Equal(999UL, config.Interval);
        Assert.Equal(5U, config.MaxAttempt);
        Assert.False(config.AutoLogin);
        Assert.Equal("Warning", config.LogLevel);
        Assert.Equal(45, config.Timeout);
        Assert.Equal(200, config.BackoffInterval);
        Assert.False(config.NotificationsEnabled);
    }

    [Fact]
    public void Load_BackwardCompatFlatKeys_Work()
    {
        var path = TempFile();
        File.WriteAllText(path, """
            username = "flat_user"
            interval = 450
            auto_login = false
            log_level = "Error"
            """);

        var config = Config.Load(path);

        Assert.Equal("flat_user", config.Username);
        Assert.Equal(450UL, config.Interval);
        Assert.False(config.AutoLogin);
        Assert.Equal("Error", config.LogLevel);
    }

    [Fact]
    public void Save_RoundTrip_PreservesValues()
    {
        var path = TempFile();

        var original = Config.Load(TempFile("nonexistent.toml"));
        original.Username = "roundtrip_user";
        original.Password = "roundtrip_pass";
        original.IpAddress = "10.0.0.99";
        original.Timeout = 25;
        original.AcceptInvalidCerts = false;
        original.Interval = 500;
        original.MaxAttempt = 15;
        original.BackoffInterval = 90;
        original.AutoLogin = false;
        original.LogLevel = "Debug";
        original.LogDirectory = "/tmp/logs";
        original.LogRetentionDays = 14;
        original.NotificationsEnabled = false;
        original.AutoUpdateCheck = false;
        original.UpdateCheckIntervalHours = 12;
        original.StartMinimized = false;

        original.Save(path);
        var loaded = Config.Load(path);

        Assert.Equal(original.Username, loaded.Username);
        Assert.Equal(original.IpAddress, loaded.IpAddress);
        Assert.Equal(original.Timeout, loaded.Timeout);
        Assert.Equal(original.AcceptInvalidCerts, loaded.AcceptInvalidCerts);
        Assert.Equal(original.Interval, loaded.Interval);
        Assert.Equal(original.MaxAttempt, loaded.MaxAttempt);
        Assert.Equal(original.BackoffInterval, loaded.BackoffInterval);
        Assert.Equal(original.AutoLogin, loaded.AutoLogin);
        Assert.Equal(original.LogLevel, loaded.LogLevel);
        Assert.Equal(original.LogDirectory, loaded.LogDirectory);
        Assert.Equal(original.LogRetentionDays, loaded.LogRetentionDays);
        Assert.Equal(original.NotificationsEnabled, loaded.NotificationsEnabled);
        Assert.Equal(original.AutoUpdateCheck, loaded.AutoUpdateCheck);
        Assert.Equal(original.UpdateCheckIntervalHours, loaded.UpdateCheckIntervalHours);
        Assert.Equal(original.StartMinimized, loaded.StartMinimized);
    }

    [Fact]
    public void Save_WithCredentialStore_RemovesPassword()
    {
        var path = TempFile();
        var store = Substitute.For<ICredentialStore>();
        store.SetPasswordAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        var config = Config.Load(TempFile("nonexistent.toml"));
        config.Username = "store_user";
        config.Password = "secret_pass";

        config.Save(path, credentialStore: store);

        store.Received(1).SetPasswordAsync("store_user", "secret_pass");

        var fileContent = File.ReadAllText(path);
        Assert.DoesNotContain("secret_pass", fileContent);
    }

    [Fact]
    public void GetPassword_FromCredentialStore_WhenConfigEmpty()
    {
        var store = Substitute.For<ICredentialStore>();
        store.GetPasswordAsync("myuser").Returns(Task.FromResult<string?>("store_password"));

        var config = Config.Load(TempFile("nonexistent.toml"));
        config.Username = "myuser";
        config.Password = null;

        var password = config.GetPassword(store);

        Assert.Equal("store_password", password);
    }

    [Fact]
    public void GetPassword_FallbackToConfigPassword()
    {
        var store = Substitute.For<ICredentialStore>();
        store.GetPasswordAsync(Arg.Any<string>()).Throws(new Exception("store unavailable"));

        var config = Config.Load(TempFile("nonexistent.toml"));
        config.Username = "fallback_user";
        config.Password = "config_password";

        var password = config.GetPassword(store);

        // Config password takes priority since it's non-empty
        Assert.Equal("config_password", password);
    }

    [Fact]
    public void GetLogDirectory_CustomDirectory_ReturnsCustom()
    {
        var config = Config.Load(TempFile("nonexistent.toml"));
        config.LogDirectory = "/custom/log/dir";

        Assert.Equal("/custom/log/dir", config.GetLogDirectory());
    }

    [Fact]
    public void GetLogDirectory_Default_ReturnsDefault()
    {
        var config = Config.Load(TempFile("nonexistent.toml"));
        config.LogDirectory = null;

        var logDir = config.GetLogDirectory();

        // Should fall back to ConfigPaths.GetLogDirectory() which returns a non-empty path
        Assert.False(string.IsNullOrEmpty(logDir));
        Assert.Contains("kmitlnetauth", logDir);
    }
}
