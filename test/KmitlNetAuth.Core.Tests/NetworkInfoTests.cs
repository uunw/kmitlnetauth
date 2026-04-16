using System.Text.RegularExpressions;
using KmitlNetAuth.Core.Platform;

namespace KmitlNetAuth.Core.Tests;

public sealed class NetworkInfoTests
{
    [Fact]
    public void GetMacAddress_ReturnsValidFormat()
    {
        var info = new NetworkInfo();

        var mac = info.GetMacAddress();

        // Should be 12 lowercase hex characters (e.g. "aabbccddeeff")
        Assert.Matches("^[0-9a-f]{12}$", mac);
    }

    [Fact]
    public void GetMacAddress_ReturnsNonEmpty()
    {
        var info = new NetworkInfo();

        var mac = info.GetMacAddress();

        Assert.False(string.IsNullOrEmpty(mac));
        // Even the fallback "000000000000" is non-empty, but let's check it's not all zeros
        // if we have a real NIC. On CI this may fall back, so we just check it's 12 chars.
        Assert.Equal(12, mac.Length);
    }
}
