using System.Net;
using System.Net.Sockets;

namespace SpideyNet.Transport;

internal static class RelayEndpointPolicy
{
    internal static bool Allows(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri)
        {
            return false;
        }

        if (endpoint.Scheme == "wss")
        {
            return true;
        }

        if (endpoint.Scheme != "ws")
        {
            return false;
        }

        if (endpoint.IsLoopback)
        {
            return true;
        }

        // Allow unencrypted play on an explicitly addressed private LAN without
        // resolving hostnames or weakening certificate validation for Internet play.
        if (!IPAddress.TryParse(endpoint.IdnHost, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return IPAddress.IsLoopback(address) || bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xFE) == 0xFC;
    }
}
