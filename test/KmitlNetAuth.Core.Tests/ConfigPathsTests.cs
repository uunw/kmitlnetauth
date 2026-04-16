using KmitlNetAuth.Core;

namespace KmitlNetAuth.Core.Tests;

public sealed class ConfigPathsTests
{
    [Fact]
    public void Resolve_ExplicitPath_ReturnsExplicitPath()
    {
        var explicit_path = "/some/custom/config.toml";

        var result = ConfigPaths.Resolve(explicit_path);

        Assert.Equal(explicit_path, result);
    }

    [Fact]
    public void Resolve_NoExplicit_ReturnsDefaultPath()
    {
        var result = ConfigPaths.Resolve();

        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("kmitlnetauth", result);
        Assert.EndsWith("config.toml", result);
    }
}
