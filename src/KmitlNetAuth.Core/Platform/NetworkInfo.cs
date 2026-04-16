using System.Net.NetworkInformation;

namespace KmitlNetAuth.Core.Platform;

public class NetworkInfo : INetworkInfo
{
    public string GetMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.GetPhysicalAddress().GetAddressBytes().Length > 0);

            if (nic == null)
                return "000000000000";

            var bytes = nic.GetPhysicalAddress().GetAddressBytes();
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return "000000000000";
        }
    }
}
