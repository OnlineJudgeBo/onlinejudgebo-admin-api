using Microsoft.AspNetCore.Http;

namespace OnlineJudgeAdminApi.Helpers;

public static class ClientIpHelper
{
    public static string GetClientIp(HttpContext httpContext)
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var rawIp = string.IsNullOrWhiteSpace(forwarded)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"
            : forwarded;

        var ip = rawIp.Split(',')[0].Trim();
        if (string.IsNullOrWhiteSpace(ip) || ip.Contains(':', StringComparison.Ordinal))
        {
            return "0.0.0.0";
        }

        return ip.Length > 15 ? "0.0.0.0" : ip;
    }
}
