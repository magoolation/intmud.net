using System.Net;

namespace IntMud.Networking;

/// <summary>
/// DNS resolution utilities.
/// </summary>
public static class DnsResolver
{
    /// <summary>
    /// Resolve a hostname to IP addresses.
    /// </summary>
    public static async Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if already an IP address
            if (IPAddress.TryParse(hostname, out var address))
            {
                return [address];
            }

            return await Dns.GetHostAddressesAsync(hostname, cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Resolve a hostname to the first IPv4 address.
    /// </summary>
    public static async Task<IPAddress?> ResolveIPv4Async(string hostname, CancellationToken cancellationToken = default)
    {
        var addresses = await ResolveAsync(hostname, cancellationToken);
        return addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
    }

    /// <summary>
    /// Resolve a hostname to the first IPv6 address.
    /// </summary>
    public static async Task<IPAddress?> ResolveIPv6Async(string hostname, CancellationToken cancellationToken = default)
    {
        var addresses = await ResolveAsync(hostname, cancellationToken);
        return addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
    }

    /// <summary>
    /// Perform reverse DNS lookup.
    /// </summary>
    public static async Task<string?> ReverseLookupAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(address.ToString());
            return entry.HostName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Perform reverse DNS lookup from string IP.
    /// </summary>
    public static async Task<string?> ReverseLookupAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
            return null;

        return await ReverseLookupAsync(address, cancellationToken);
    }
}
