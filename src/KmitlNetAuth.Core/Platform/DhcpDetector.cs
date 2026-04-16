using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;

namespace KmitlNetAuth.Core.Platform;

/// <summary>
/// Detects whether the active network interface is using DHCP
/// and retrieves the current IPv4 address.
/// </summary>
public static class DhcpDetector
{
    public static (bool IsDhcp, string CurrentIp) GetNetworkStatus()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            if (nic == null)
                return (false, "");

            var ipProps = nic.GetIPProperties();
            var ipv4 = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4 == null)
                return (false, "");

            var currentIp = ipv4.Address.ToString();
            var isDhcp = DetectDhcp(ipProps);

            return (isDhcp, currentIp);
        }
        catch
        {
            return (false, "");
        }
    }

    private static bool DetectDhcp(IPInterfaceProperties ipProps)
    {
        // Windows: use the dedicated API
        if (OperatingSystem.IsWindows())
            return DetectDhcpWindows(ipProps);

        // Linux: check for configured DHCP servers
        if (OperatingSystem.IsLinux())
            return DetectDhcpLinux(ipProps);

        // Other platforms: assume not DHCP (cannot reliably detect)
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool DetectDhcpWindows(IPInterfaceProperties ipProps)
    {
        try
        {
            return ipProps.GetIPv4Properties().IsDhcpEnabled;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("linux")]
    private static bool DetectDhcpLinux(IPInterfaceProperties ipProps)
    {
        try
        {
            return ipProps.DhcpServerAddresses.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
