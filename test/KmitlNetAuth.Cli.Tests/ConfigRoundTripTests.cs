using KmitlNetAuth.Core;

namespace KmitlNetAuth.Cli.Tests;

public class ConfigRoundTripTests
{
    [Fact]
    public void Config_SaveAndLoad_PreservesAllValues()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"kmitl_test_{Guid.NewGuid()}.toml");
        try
        {
            var original = new Config
            {
                Username = "testuser",
                Password = "secret123",
                IpAddress = "10.0.0.42",
                PortalUrl = "https://portal.example.com/login",
                HeartbeatUrl = "https://heartbeat.example.com/api",
                InternetCheckUrl = "http://check.example.com/ok",
                Timeout = 30,
                AcceptInvalidCerts = false,
                HeartbeatUserAgent = "TestAgent/1.0",
                Interval = 600,
                MaxAttempt = 10,
                BackoffInterval = 120,
                AutoLogin = false,
                LogLevel = "Debug",
                LogDirectory = "/tmp/testlogs",
                LogRetentionDays = 7,
                NotificationsEnabled = false,
                AutoUpdateCheck = false,
                UpdateCheckIntervalHours = 48,
                StartMinimized = false,
            };

            // Save without credential store so password stays in TOML
            original.Save(tmpFile);

            var loaded = Config.Load(tmpFile);

            Assert.Equal(original.Username, loaded.Username);
            Assert.Equal(original.IpAddress, loaded.IpAddress);
            Assert.Equal(original.PortalUrl, loaded.PortalUrl);
            Assert.Equal(original.HeartbeatUrl, loaded.HeartbeatUrl);
            Assert.Equal(original.InternetCheckUrl, loaded.InternetCheckUrl);
            Assert.Equal(original.Timeout, loaded.Timeout);
            Assert.Equal(original.AcceptInvalidCerts, loaded.AcceptInvalidCerts);
            Assert.Equal(original.HeartbeatUserAgent, loaded.HeartbeatUserAgent);
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
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void Config_SaveAndLoad_TomlFormat()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"kmitl_test_{Guid.NewGuid()}.toml");
        try
        {
            var config = new Config { Username = "fmtuser" };
            config.Save(tmpFile);

            var content = File.ReadAllText(tmpFile);

            Assert.Contains("[auth]", content);
            Assert.Contains("[network]", content);
            Assert.Contains("[service]", content);
            Assert.Contains("[logging]", content);
            Assert.Contains("[notifications]", content);
            Assert.Contains("[update]", content);
            Assert.Contains("[tray]", content);
            Assert.Contains("username = \"fmtuser\"", content);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void Config_DefaultPath_IsToml()
    {
        var path = ConfigPaths.Resolve();

        Assert.EndsWith(".toml", path);
    }
}
