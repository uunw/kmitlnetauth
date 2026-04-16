using KmitlNetAuth.Core.Platform.Linux;

namespace KmitlNetAuth.Core.Tests;

public sealed class SkipOnWindowsFactAttribute : FactAttribute
{
    public SkipOnWindowsFactAttribute()
    {
        if (OperatingSystem.IsWindows())
            Skip = "Test only runs on Unix systems";
    }
}

public sealed class FileCredentialStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalXdgConfig;

    public FileCredentialStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"kmitl_cred_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Redirect ConfigPaths.GetCredentialPath() to our temp dir via XDG_CONFIG_HOME
        _originalXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _originalXdgConfig);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SetAndGet_RoundTrip_ReturnsPassword()
    {
        var store = new FileCredentialStore();

        await store.SetPasswordAsync("testuser", "secretpass123");
        var password = await store.GetPasswordAsync("testuser");

        Assert.Equal("secretpass123", password);
    }

    [Fact]
    public async Task GetPassword_NotFound_ReturnsNull()
    {
        var store = new FileCredentialStore();

        var password = await store.GetPasswordAsync("nonexistent_user");

        Assert.Null(password);
    }

    [Fact]
    public async Task DeletePassword_RemovesEntry()
    {
        var store = new FileCredentialStore();

        await store.SetPasswordAsync("deleteuser", "toremove");
        var before = await store.GetPasswordAsync("deleteuser");
        Assert.Equal("toremove", before);

        await store.DeletePasswordAsync("deleteuser");
        var after = await store.GetPasswordAsync("deleteuser");

        Assert.Null(after);
    }

    [SkipOnWindowsFact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416")]
    public async Task SetPassword_FilePermissions_AreRestricted()
    {
        var store = new FileCredentialStore();
        await store.SetPasswordAsync("permuser", "permpass");

        var credPath = KmitlNetAuth.Core.ConfigPaths.GetCredentialPath();
        Assert.True(File.Exists(credPath));

        var mode = File.GetUnixFileMode(credPath);
        // Should only have owner read + write (0600)
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }
}
