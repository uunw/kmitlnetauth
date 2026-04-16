using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

namespace KmitlNetAuth.Core;

public sealed class Config
{
    // [auth]
    public string Username { get; set; } = "";
    public string? Password { get; set; }
    public string? IpAddress { get; set; }
    public string PortalUrl { get; set; } = "https://portal.kmitl.ac.th:19008/portalauth/login";
    public string HeartbeatUrl { get; set; } = "https://nani.csc.kmitl.ac.th/network-api/data/";
    public string InternetCheckUrl { get; set; } = "http://detectportal.firefox.com/success.txt";

    // [network]
    public int Timeout { get; set; } = 10;
    public bool AcceptInvalidCerts { get; set; } = true;
    public string HeartbeatUserAgent { get; set; } = "Chrome v116.0.5845.141 on Windows 10 64-bit";

    // [service]
    public ulong Interval { get; set; } = 300;
    public uint MaxAttempt { get; set; } = 20;
    public int BackoffInterval { get; set; } = 60;
    public bool AutoLogin { get; set; } = true;

    // [logging]
    public string LogLevel { get; set; } = "Information";
    public string? LogDirectory { get; set; }
    public int LogRetentionDays { get; set; } = 30;

    // [notifications]
    public bool NotificationsEnabled { get; set; } = true;

    // [update]
    public bool AutoUpdateCheck { get; set; } = true;
    public int UpdateCheckIntervalHours { get; set; } = 24;

    // [tray]
    public bool StartMinimized { get; set; } = true;

    public static Config Load(string path, ICredentialStore? credentialStore = null, ILogger? logger = null)
    {
        var config = new Config();

        // Try TOML first, then YAML for backward compatibility
        if (File.Exists(path))
        {
            var content = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    LoadFromToml(config, content);
                }
                catch (Exception e)
                {
                    logger?.LogWarning("Failed to parse config (using defaults): {Error}", e.Message);
                    config = new Config();
                }
            }
        }
        else
        {
            // Check for legacy YAML config and migrate
            var yamlPath = Path.ChangeExtension(path, ".yaml");
            if (File.Exists(yamlPath))
            {
                logger?.LogInformation("Found legacy config.yaml, migrating to config.toml...");
                MigrateFromYaml(config, yamlPath, logger);
            }
        }

        ApplyEnvironmentOverrides(config);
        MigrateCredentials(config, credentialStore, logger);

        return config;
    }

    public void Save(string path, ICredentialStore? credentialStore = null, ILogger? logger = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var configToSave = Clone();

        if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(Username) && credentialStore != null)
        {
            try
            {
                credentialStore.SetPasswordAsync(Username, Password).GetAwaiter().GetResult();
                configToSave.Password = null;
            }
            catch (Exception e)
            {
                logger?.LogWarning("Could not save password to credential store: {Error}", e.Message);
            }
        }

        var toml = SerializeToToml(configToSave);
        File.WriteAllText(path, toml);
    }

    public string GetPassword(ICredentialStore? credentialStore)
    {
        if (!string.IsNullOrEmpty(Password))
            return Password;

        if (!string.IsNullOrEmpty(Username) && credentialStore != null)
        {
            try
            {
                var pwd = credentialStore.GetPasswordAsync(Username).GetAwaiter().GetResult();
                if (pwd != null)
                    return pwd;
            }
            catch { }
        }

        return "";
    }

    public string GetLogDirectory()
    {
        if (!string.IsNullOrEmpty(LogDirectory))
            return LogDirectory;
        return ConfigPaths.GetLogDirectory();
    }

    private Config Clone() => new()
    {
        Username = Username,
        Password = Password,
        IpAddress = IpAddress,
        PortalUrl = PortalUrl,
        HeartbeatUrl = HeartbeatUrl,
        InternetCheckUrl = InternetCheckUrl,
        Timeout = Timeout,
        AcceptInvalidCerts = AcceptInvalidCerts,
        HeartbeatUserAgent = HeartbeatUserAgent,
        Interval = Interval,
        MaxAttempt = MaxAttempt,
        BackoffInterval = BackoffInterval,
        AutoLogin = AutoLogin,
        LogLevel = LogLevel,
        LogDirectory = LogDirectory,
        LogRetentionDays = LogRetentionDays,
        NotificationsEnabled = NotificationsEnabled,
        AutoUpdateCheck = AutoUpdateCheck,
        UpdateCheckIntervalHours = UpdateCheckIntervalHours,
        StartMinimized = StartMinimized,
    };

    private static void LoadFromToml(Config config, string content)
    {
        var table = TomlSerializer.Deserialize<TomlTable>(content)
            ?? throw new InvalidOperationException("Failed to parse TOML config");

        if (GetSection(table, "auth") is { } auth)
        {
            config.Username = GetString(auth, "username", config.Username);
            config.Password = GetString(auth, "password", config.Password);
            config.IpAddress = NullIfEmpty(GetString(auth, "ip_address", config.IpAddress));
            config.PortalUrl = GetString(auth, "portal_url", config.PortalUrl);
            config.HeartbeatUrl = GetString(auth, "heartbeat_url", config.HeartbeatUrl);
            config.InternetCheckUrl = GetString(auth, "internet_check_url", config.InternetCheckUrl);
        }

        if (GetSection(table, "network") is { } net)
        {
            config.Timeout = GetInt(net, "timeout", config.Timeout);
            config.AcceptInvalidCerts = GetBool(net, "accept_invalid_certs", config.AcceptInvalidCerts);
            config.HeartbeatUserAgent = GetString(net, "heartbeat_user_agent", config.HeartbeatUserAgent);
        }

        if (GetSection(table, "service") is { } svc)
        {
            config.Interval = (ulong)GetInt(svc, "interval", (int)config.Interval);
            config.MaxAttempt = (uint)GetInt(svc, "max_attempt", (int)config.MaxAttempt);
            config.BackoffInterval = GetInt(svc, "backoff_interval", config.BackoffInterval);
            config.AutoLogin = GetBool(svc, "auto_login", config.AutoLogin);
        }

        if (GetSection(table, "logging") is { } log)
        {
            config.LogLevel = GetString(log, "level", config.LogLevel);
            config.LogDirectory = NullIfEmpty(GetString(log, "directory", config.LogDirectory));
            config.LogRetentionDays = GetInt(log, "retention_days", config.LogRetentionDays);
        }

        if (GetSection(table, "notifications") is { } notif)
            config.NotificationsEnabled = GetBool(notif, "enabled", config.NotificationsEnabled);

        if (GetSection(table, "update") is { } upd)
        {
            config.AutoUpdateCheck = GetBool(upd, "auto_check", config.AutoUpdateCheck);
            config.UpdateCheckIntervalHours = GetInt(upd, "check_interval_hours", config.UpdateCheckIntervalHours);
        }

        if (GetSection(table, "tray") is { } tray)
            config.StartMinimized = GetBool(tray, "start_minimized", config.StartMinimized);

        // Flat keys for backward compatibility
        config.Username = GetString(table, "username", config.Username);
        config.Interval = (ulong)GetInt(table, "interval", (int)config.Interval);
        config.AutoLogin = GetBool(table, "auto_login", config.AutoLogin);
        config.LogLevel = GetString(table, "log_level", config.LogLevel);
    }

    private static TomlTable? GetSection(TomlTable table, string key) =>
        table.TryGetValue(key, out var obj) && obj is TomlTable t ? t : null;

    private static string GetString(TomlTable table, string key, string? fallback) =>
        table.TryGetValue(key, out var v) && v != null ? v.ToString()! : fallback ?? "";

    private static int GetInt(TomlTable table, string key, int fallback) =>
        table.TryGetValue(key, out var v) && v is long l ? (int)l : fallback;

    private static bool GetBool(TomlTable table, string key, bool fallback) =>
        table.TryGetValue(key, out var v) && v is bool b ? b : fallback;

    private static string SerializeToToml(Config config)
    {
        return $"""
            # KMITL NetAuth Configuration

            [auth]
            username = "{config.Username}"
            ip_address = "{config.IpAddress ?? ""}"
            portal_url = "{config.PortalUrl}"
            heartbeat_url = "{config.HeartbeatUrl}"
            internet_check_url = "{config.InternetCheckUrl}"

            [network]
            timeout = {config.Timeout}
            accept_invalid_certs = {config.AcceptInvalidCerts.ToString().ToLowerInvariant()}
            heartbeat_user_agent = "{config.HeartbeatUserAgent}"

            [service]
            interval = {config.Interval}
            max_attempt = {config.MaxAttempt}
            backoff_interval = {config.BackoffInterval}
            auto_login = {config.AutoLogin.ToString().ToLowerInvariant()}

            [logging]
            level = "{config.LogLevel}"
            directory = "{config.LogDirectory ?? ""}"
            retention_days = {config.LogRetentionDays}

            [notifications]
            enabled = {config.NotificationsEnabled.ToString().ToLowerInvariant()}

            [update]
            auto_check = {config.AutoUpdateCheck.ToString().ToLowerInvariant()}
            check_interval_hours = {config.UpdateCheckIntervalHours}

            [tray]
            start_minimized = {config.StartMinimized.ToString().ToLowerInvariant()}
            """;
    }

    private static void MigrateFromYaml(Config config, string yamlPath, ILogger? logger)
    {
        try
        {
            var lines = File.ReadAllLines(yamlPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var parts = trimmed.Split(':', 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');

                switch (key)
                {
                    case "username": config.Username = value; break;
                    case "password": config.Password = value; break;
                    case "ip_address": config.IpAddress = NullIfEmpty(value); break;
                    case "interval" when ulong.TryParse(value, out var i): config.Interval = i; break;
                    case "max_attempt" when uint.TryParse(value, out var m): config.MaxAttempt = m; break;
                    case "auto_login" when bool.TryParse(value, out var a): config.AutoLogin = a; break;
                    case "log_level": config.LogLevel = value; break;
                }
            }

            logger?.LogInformation("Migration from YAML complete");
        }
        catch (Exception e)
        {
            logger?.LogWarning("Failed to migrate from YAML: {Error}", e.Message);
        }
    }

    private static void ApplyEnvironmentOverrides(Config config)
    {
        var val = Environment.GetEnvironmentVariable("KMITL_USERNAME");
        if (val != null) config.Username = val;

        val = Environment.GetEnvironmentVariable("KMITL_PASSWORD");
        if (val != null) config.Password = val;

        val = Environment.GetEnvironmentVariable("KMITL_IP");
        if (val != null) config.IpAddress = NullIfEmpty(val);

        val = Environment.GetEnvironmentVariable("KMITL_INTERVAL");
        if (val != null && ulong.TryParse(val, out var interval))
            config.Interval = interval;

        val = Environment.GetEnvironmentVariable("KMITL_MAX_ATTEMPT");
        if (val != null && uint.TryParse(val, out var maxAttempt))
            config.MaxAttempt = maxAttempt;

        val = Environment.GetEnvironmentVariable("KMITL_AUTO_LOGIN");
        if (val != null && bool.TryParse(val, out var autoLogin))
            config.AutoLogin = autoLogin;

        val = Environment.GetEnvironmentVariable("KMITL_LOG_LEVEL");
        if (val != null) config.LogLevel = val;

        val = Environment.GetEnvironmentVariable("KMITL_TIMEOUT");
        if (val != null && int.TryParse(val, out var timeout))
            config.Timeout = timeout;

        val = Environment.GetEnvironmentVariable("KMITL_BACKOFF_INTERVAL");
        if (val != null && int.TryParse(val, out var backoff))
            config.BackoffInterval = backoff;

        val = Environment.GetEnvironmentVariable("KMITL_NOTIFICATIONS");
        if (val != null && bool.TryParse(val, out var notif))
            config.NotificationsEnabled = notif;
    }

    private static void MigrateCredentials(Config config, ICredentialStore? credentialStore, ILogger? logger)
    {
        if (string.IsNullOrEmpty(config.Password) || string.IsNullOrEmpty(config.Username) || credentialStore == null)
            return;

        try
        {
            credentialStore.SetPasswordAsync(config.Username, config.Password).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            logger?.LogWarning("Failed to migrate password to credential store: {Error}", e.Message);
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
