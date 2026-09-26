using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Sockets;

namespace OnlineJudgeAdminApi.Helpers;

public static class ClientIpHelper
{
    private static readonly IPNetwork DockerNetwork = IPNetwork.Parse("172.16.0.0/12");

    public static string GetClientIp(HttpContext httpContext)
    {
        var remote = httpContext.Connection.RemoteIpAddress;
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var rawIp = remote != null && DockerNetwork.Contains(remote) && !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : remote?.ToString();

        if (!IPAddress.TryParse(rawIp, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return "0.0.0.0";
        }

        return ip.ToString();
    }
}
