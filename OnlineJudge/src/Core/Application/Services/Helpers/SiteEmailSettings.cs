using Microsoft.Extensions.Configuration;

namespace OnlineJudgeAdmin.Core.Application.Services.Helpers;

// One API serves several domains (sites). Each site can override any email setting under
// Email:Sites:{siteId}:<key> (env: Email__Sites__2__Smtp__Host); missing keys fall back to Email:<key>,
// then to the legacy flat keys.
public static class SiteEmailSettings
{
    public static string? Get(IConfiguration configuration, int siteId, string key, params string[] legacyKeys)
    {
        foreach (var candidate in new[] { $"Email:Sites:{siteId}:{key}", $"Email:{key}" }.Concat(legacyKeys))
        {
            var value = configuration[candidate];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
