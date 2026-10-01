using System.Net;
using System.Net.Sockets;

namespace UrlShortener.Core.Links;

/// <summary>Classifies IP addresses that must not be the target of a public short link.</summary>
public static class NetworkAddress
{
    public static bool IsPrivateOrLocal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        // An IPv4 address hidden in IPv6 (::ffff:192.168.0.1) is judged as the IPv4 address it is.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast))
        {
            return true;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateOrLocalIPv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || address.IsIPv6UniqueLocal,
            _ => true,   // anything unexpected is not a public internet address
        };
    }

    private static bool IsPrivateOrLocalIPv4(byte[] b) =>
        b[0] == 0                                    // 0.0.0.0/8 "this network"
        || b[0] == 10                                // 10.0.0.0/8 private
        || (b[0] == 100 && (b[1] & 0xC0) == 64)      // 100.64.0.0/10 carrier-grade NAT
        || b[0] == 127                               // 127.0.0.0/8 loopback
        || (b[0] == 169 && b[1] == 254)              // 169.254.0.0/16 link-local (incl. cloud metadata endpoints)
        || (b[0] == 172 && (b[1] & 0xF0) == 16)      // 172.16.0.0/12 private
        || (b[0] == 192 && b[1] == 168)              // 192.168.0.0/16 private
        || b[0] >= 224;                              // 224.0.0.0/4 multicast and 240.0.0.0/4 reserved
}
