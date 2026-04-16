using KmitlNetAuth.Core.Exceptions;
using KmitlNetAuth.Core.Platform;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KmitlNetAuth.Core;

public sealed class Config
{
    [YamlMember(Alias = "username")]
    public string Username { get; set; } = "";

    [YamlMember(Alias = "password")]
    public string? Password { get; set; }

    [YamlMember(Alias = "ip_address")]
    public string? IpAddress { get; set; }

    [YamlMember(Alias = "interval")]
    public ulong Interval { get; set; } = 300;

    [YamlMember(Alias = "max_attempt")]
    public uint MaxAttempt { get; set; } = 20;

    [YamlMember(Alias = "auto_login")]
    public bool AutoLogin { get; set; } = true;

    [YamlMember(Alias = "log_level")]
    public string LogLevel { get; set; } = "Information";

    public static Config Load(string path, ICredentialStore? credentialStore = null, ILogger? logger = null)
    {
        Config config;

        if (File.Exists(path))
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                config = new Config();
            }
            else
            {
                try
                {
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();
                    config = deserializer.Deserialize<Config>(content) ?? new Config();
                }
                catch (Exception e)
                {
                    logger?.LogWarning("Failed to parse YAML config (using defaults): {Error}", e.Message);
                    config = new Config();
                }
            }
        }
        else
        {
            config = new Config();
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

        var configToSave = new Config
        {
            Username = Username,
            Password = Password,
            IpAddress = IpAddress,
            Interval = Interval,
            MaxAttempt = MaxAttempt,
            AutoLogin = AutoLogin,
            LogLevel = LogLevel,
        };

        if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(Username) && credentialStore != null)
        {
            try
            {
                credentialStore.SetPasswordAsync(Username, Password).GetAwaiter().GetResult();
                configToSave.Password = null;
            }
            catch (Exception e)
            {
                logger?.LogWarning("Could not save password to credential store, falling back to file: {Error}", e.Message);
            }
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        var yaml = serializer.Serialize(configToSave);
        File.WriteAllText(path, yaml);
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
            catch
            {
                // Credential store not available, fall through
            }
        }

        return "";
    }

    private static void ApplyEnvironmentOverrides(Config config)
    {
        var val = Environment.GetEnvironmentVariable("KMITL_USERNAME");
        if (val != null) config.Username = val;

        val = Environment.GetEnvironmentVariable("KMITL_PASSWORD");
        if (val != null) config.Password = val;

        val = Environment.GetEnvironmentVariable("KMITL_IP");
        if (val != null) config.IpAddress = val;

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
}
