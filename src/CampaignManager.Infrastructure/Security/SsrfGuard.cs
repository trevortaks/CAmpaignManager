using System.Net;
using System.Net.Sockets;

namespace CampaignManager.Infrastructure.Security;

/// <summary>Blocks callback URLs that resolve to private, loopback, link-local or otherwise
/// non-public addresses, preventing server-side request forgery via campaign CallbackUrl.</summary>
public static class SsrfGuard
{
    public static async Task<bool> IsSafePublicUrlAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        return addresses.Length > 0 && addresses.All(IsPublic);
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 when bytes[1] >= 64 && bytes[1] <= 127 => false,  // CGNAT 100.64/10
                169 when bytes[1] == 254 => false,                     // link-local
                172 when bytes[1] >= 16 && bytes[1] <= 31 => false,
                192 when bytes[1] == 168 => false,
                192 when bytes[1] == 0 && bytes[2] == 2 => false,      // TEST-NET
                198 when bytes[1] >= 18 && bytes[1] <= 19 => false,
                >= 224 => false,                                        // multicast/reserved
                _ => true
            };
        }

        // IPv6: reject unique-local (fc00::/7), link-local (fe80::/10), multicast, unspecified.
        if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
        if (address.Equals(IPAddress.IPv6Any)) return false;
        var first = address.GetAddressBytes()[0];
        return (first & 0xFE) != 0xFC;
    }
}
