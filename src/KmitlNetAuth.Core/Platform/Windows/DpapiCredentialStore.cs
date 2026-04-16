using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KmitlNetAuth.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public class DpapiCredentialStore : ICredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("kmitlnetauth");

    public Task SetPasswordAsync(string username, string password)
    {
        var plainBytes = Encoding.UTF8.GetBytes(password);
        var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        var base64 = Convert.ToBase64String(encrypted);

        var data = new Dictionary<string, string> { [username] = base64 };
        var json = JsonSerializer.Serialize(data, CredentialJsonContext.Default.DictionaryStringString);

        var path = ConfigPaths.GetCredentialPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, json);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordAsync(string username)
    {
        var path = ConfigPaths.GetCredentialPath();
        if (!File.Exists(path))
            return Task.FromResult<string?>(null);

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize(json, CredentialJsonContext.Default.DictionaryStringString);

        if (data == null || !data.TryGetValue(username, out var base64))
            return Task.FromResult<string?>(null);

        var encrypted = Convert.FromBase64String(base64);
        var plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return Task.FromResult<string?>(Encoding.UTF8.GetString(plainBytes));
    }

    public Task DeletePasswordAsync(string username)
    {
        var path = ConfigPaths.GetCredentialPath();
        if (!File.Exists(path))
            return Task.CompletedTask;

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize(json, CredentialJsonContext.Default.DictionaryStringString);

        if (data != null && data.Remove(username))
        {
            var updatedJson = JsonSerializer.Serialize(data, CredentialJsonContext.Default.DictionaryStringString);
            File.WriteAllText(path, updatedJson);
        }

        return Task.CompletedTask;
    }
}
