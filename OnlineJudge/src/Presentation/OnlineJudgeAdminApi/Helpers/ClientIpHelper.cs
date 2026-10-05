using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;

namespace OnlineJudgeAdminApi.Helpers;

public static class ClientIpHelper
{
    private static readonly IPNetwork[] DockerNetwork = [IPNetwork.Parse("172.16.0.0/12")];

    public static string GetClientIp(HttpContext httpContext)
    {
        var remote = httpContext.Connection.RemoteIpAddress;
        // A dual-stack listener reports IPv4 clients as ::ffff:a.b.c.d.
        if (remote is { IsIPv4MappedToIPv6: true })
        {
            remote = remote.MapToIPv4();
        }

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var rawIp = remote != null && TrustedProxies(httpContext).Any(network => network.Contains(remote)) && !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : remote?.ToString();

        if (!IPAddress.TryParse(rawIp, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return "0.0.0.0";
        }

        return ip.ToString();
    }

    // Only these peers may set X-Forwarded-For. ClientIp:TrustedProxies ("172.20.0.0/16, 10.0.0.5")
    // narrows or moves it to the reverse proxy's real network; default is Docker's address pool.
    private static IReadOnlyList<IPNetwork> TrustedProxies(HttpContext httpContext)
    {
        var configured = httpContext.RequestServices?.GetService<IConfiguration>()?["ClientIp:TrustedProxies"];
        var networks = (configured ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => IPNetwork.TryParse(entry, out var network) ? network
                : IPAddress.TryParse(entry, out var address) ? new IPNetwork(address, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128)
                : (IPNetwork?)null)
            .OfType<IPNetwork>()
            .ToList();
        return networks.Count > 0 ? networks : DockerNetwork;
    }
}
