using KmitlNetAuth.Core.Platform;

namespace KmitlNetAuth.Core.Tests;

public sealed class DhcpDetectorTests
{
    [Fact]
    public void GetNetworkStatus_ReturnsResult()
    {
        var (isDhcp, currentIp) = DhcpDetector.GetNetworkStatus();

        // Smoke test: method should not throw and should return a valid tuple
        Assert.IsType<bool>(isDhcp);
        Assert.NotNull(currentIp);
    }
}
