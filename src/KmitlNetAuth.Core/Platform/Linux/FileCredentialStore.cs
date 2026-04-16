using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KmitlNetAuth.Core.Platform.Linux;

public class FileCredentialStore : ICredentialStore
{
    private const string Salt = "kmitlnetauth";

    public Task SetPasswordAsync(string username, string password)
    {
        var key = DeriveKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(password);
        var encrypted = aes.EncryptCbc(plainBytes, aes.IV, PaddingMode.PKCS7);

        var payload = new CredentialPayload
        {
            Iv = Convert.ToBase64String(aes.IV),
            Data = Convert.ToBase64String(encrypted),
        };

        var existing = LoadAll();
        existing[username] = payload;

        var path = ConfigPaths.GetCredentialPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(existing);
        File.WriteAllText(path, json);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordAsync(string username)
    {
        var existing = LoadAll();
        if (!existing.TryGetValue(username, out var payload))
            return Task.FromResult<string?>(null);

        var key = DeriveKey();
        var iv = Convert.FromBase64String(payload.Iv);
        var encrypted = Convert.FromBase64String(payload.Data);

        using var aes = Aes.Create();
        aes.Key = key;

        var plainBytes = aes.DecryptCbc(encrypted, iv, PaddingMode.PKCS7);
        return Task.FromResult<string?>(Encoding.UTF8.GetString(plainBytes));
    }

    public Task DeletePasswordAsync(string username)
    {
        var existing = LoadAll();
        if (existing.Remove(username))
        {
            var path = ConfigPaths.GetCredentialPath();
            var json = JsonSerializer.Serialize(existing);
            File.WriteAllText(path, json);
        }

        return Task.CompletedTask;
    }

    private static byte[] DeriveKey()
    {
        var machineId = GetMachineId();
        var passwordBytes = Encoding.UTF8.GetBytes(machineId);
        var saltBytes = Encoding.UTF8.GetBytes(Salt);
        return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
    }

    private static string GetMachineId()
    {
        // Try /etc/machine-id (systemd)
        const string machineIdPath = "/etc/machine-id";
        if (File.Exists(machineIdPath))
            return File.ReadAllText(machineIdPath).Trim();

        // Fallback to hostname
        return Environment.MachineName;
    }

    private static Dictionary<string, CredentialPayload> LoadAll()
    {
        var path = ConfigPaths.GetCredentialPath();
        if (!File.Exists(path))
            return new Dictionary<string, CredentialPayload>();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, CredentialPayload>>(json)
            ?? new Dictionary<string, CredentialPayload>();
    }

    private sealed class CredentialPayload
    {
        public string Iv { get; set; } = "";
        public string Data { get; set; } = "";
    }
}
