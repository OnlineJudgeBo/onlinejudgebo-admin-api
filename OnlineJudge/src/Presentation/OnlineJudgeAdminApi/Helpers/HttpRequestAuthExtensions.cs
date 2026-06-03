using System.Net.Http.Headers;

namespace OnlineJudgeAdminApi.Helpers;

public static class HttpRequestAuthExtensions
{
    public static string GetBearerToken(this HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return header.Parameter?.Trim() ?? string.Empty;
    }
}
