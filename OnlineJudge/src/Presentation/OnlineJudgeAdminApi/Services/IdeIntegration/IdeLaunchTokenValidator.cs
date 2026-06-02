using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed class IdeLaunchTokenValidator : IIdeLaunchTokenValidator
{
    private const string DefaultIssuer = "patito-online-judge";
    private const string DefaultAudience = "vibe-ide";

    private readonly IConfiguration _configuration;

    public IdeLaunchTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IdeLaunchClaims Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Missing IDE launch token.");
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException("Invalid IDE launch token format.");
        }

        ValidateSignature(parts);
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
        var root = document.RootElement;

        if (GetString(root, "iss") != TokenIssuer() || GetString(root, "aud") != TokenAudience())
        {
            throw new ArgumentException("Invalid IDE launch token issuer or audience.");
        }

        if (GetInt64(root, "exp") < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            throw new ArgumentException("Expired IDE launch token.");
        }

        var userId = GetString(root, "sub");
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Missing user claim.");
        }

        return new IdeLaunchClaims(
            userId,
            GetInt32(root, "site_id"),
            GetInt32(root, "problem_id"),
            GetNullableInt32(root, "contest_id"),
            GetNullableInt32(root, "num"),
            GetIntArray(root, "allowed_languages"));
    }

    private void ValidateSignature(IReadOnlyList<string> tokenParts)
    {
        var unsignedToken = $"{tokenParts[0]}.{tokenParts[1]}";
        var expectedSignature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TokenSecret()), Encoding.UTF8.GetBytes(unsignedToken)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedSignature), Encoding.ASCII.GetBytes(tokenParts[2])))
        {
            throw new ArgumentException("Invalid IDE launch token signature.");
        }
    }

    private string TokenSecret()
    {
        var secret = Environment.GetEnvironmentVariable("VIBE_IDE_TOKEN_SECRET")
            ?? _configuration["VibeIde:TokenSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("VIBE_IDE_TOKEN_SECRET must be configured with at least 32 characters.");
        }
        return secret;
    }

    private string TokenIssuer()
    {
        return Environment.GetEnvironmentVariable("VIBE_IDE_TOKEN_ISS")
            ?? _configuration["VibeIde:TokenIssuer"]
            ?? DefaultIssuer;
    }

    private string TokenAudience()
    {
        return Environment.GetEnvironmentVariable("VIBE_IDE_TOKEN_AUD")
            ?? _configuration["VibeIde:TokenAudience"]
            ?? DefaultAudience;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int GetInt32(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) ? result : 0;
    }

    private static int? GetNullableInt32(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static long GetInt64(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var result) ? result : 0;
    }

    private static int[] GetIntArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        return value.EnumerateArray()
            .Where(item => item.TryGetInt32(out _))
            .Select(item => item.GetInt32())
            .ToArray();
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
