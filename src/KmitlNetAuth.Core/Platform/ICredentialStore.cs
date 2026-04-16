namespace KmitlNetAuth.Core.Platform;

public interface ICredentialStore
{
    Task SetPasswordAsync(string username, string password);
    Task<string?> GetPasswordAsync(string username);
    Task DeletePasswordAsync(string username);
}
