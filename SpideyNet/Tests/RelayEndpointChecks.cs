using SpideyNet.Transport;

namespace SpideyNet.Tests;

internal static class RelayEndpointChecks
{
    internal static void Run()
    {
        foreach (var endpoint in new[]
        {
            "ws://localhost:5000", "ws://127.0.0.1:5000", "ws://[::1]:5000",
            "ws://192.168.178.45:5000", "ws://10.0.0.1:5000", "ws://172.16.0.1:5000",
            "ws://172.31.255.254:5000", "ws://[fd12::1]:5000", "ws://[fc00::1]:5000",
            "ws://[::ffff:192.168.178.45]:5000", "wss://relay.example:5000", "wss://192.168.178.45:5000"
        })
        {
            if (!RelayEndpointPolicy.Allows(new Uri(endpoint)))
            {
                throw new InvalidOperationException($"A supported relay address was rejected: {endpoint}");
            }
        }

        // Adjacent public ranges and misleading hostnames must still require TLS.
        foreach (var endpoint in new[]
        {
            "ws://172.15.255.255:5000", "ws://172.32.0.1:5000", "ws://192.169.0.1:5000",
            "ws://8.8.8.8:5000", "ws://[2001:db8::1]:5000", "ws://relay.example:5000",
            "ws://192.168.178.45.example:5000", "http://192.168.178.45:5000", "/relative"
        })
        {
            if (RelayEndpointPolicy.Allows(new Uri(endpoint, UriKind.RelativeOrAbsolute)))
            {
                throw new InvalidOperationException($"An unsupported relay address was accepted: {endpoint}");
            }
        }

        Console.WriteLine("Relay endpoint checks: private LAN WebSockets allowed; public relays require TLS.");
    }
}
