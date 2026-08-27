using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnlineJudgeAdmin.Infrastructure.IdeIntegration.Implementations;

public sealed class IdeLaunchTokenValidator : IIdeLaunchTokenValidator
{
    private const string DefaultIssuer = "patito-online-judge";
    private const string DefaultAudience = "patito-ide";

    private readonly IConfiguration _configuration;

    public IdeLaunchTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IdeLaunchClaims Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Falta el token de lanzamiento de IDE.");
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException("Formato de token de lanzamiento de IDE inválido.");
        }

        ValidateSignature(parts);
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
        var root = document.RootElement;

        if (GetString(root, "iss") != TokenIssuer() || GetString(root, "aud") != TokenAudience())
        {
            throw new ArgumentException("Emisor o audiencia del token de lanzamiento de IDE inválido.");
        }

        if (GetInt64(root, "exp") < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            throw new ArgumentException("El token de lanzamiento de IDE expiró.");
        }

        var userId = GetString(root, "sub");
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Falta el claim de usuario.");
        }

        return new IdeLaunchClaims(
            userId,
            GetInt32(root, "site_id"),
            GetInt32(root, "problem_id"),
            GetNullableInt32(root, "contest_id"),
            GetNullableInt32(root, "num"),
            GetIntArray(root, "allowed_languages"),
            GetNullableInt64(root, "course_id"),
            GetNullableInt64(root, "assignment_id"));
    }

    private void ValidateSignature(IReadOnlyList<string> tokenParts)
    {
        var unsignedToken = $"{tokenParts[0]}.{tokenParts[1]}";
        var expectedSignature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TokenSecret()), Encoding.UTF8.GetBytes(unsignedToken)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedSignature), Encoding.ASCII.GetBytes(tokenParts[2])))
        {
            throw new ArgumentException("Firma del token de lanzamiento de IDE inválida.");
        }
    }

    private string TokenSecret()
    {
        var secret = Environment.GetEnvironmentVariable("PATITO_IDE_TOKEN_SECRET")
            ?? _configuration["PatitoIde:TokenSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("PATITO_IDE_TOKEN_SECRET must be configured with at least 32 characters.");
        }
        return secret;
    }

    private string TokenIssuer()
    {
        return Environment.GetEnvironmentVariable("PATITO_IDE_TOKEN_ISS")
            ?? _configuration["PatitoIde:TokenIssuer"]
            ?? DefaultIssuer;
    }

    private string TokenAudience()
    {
        return Environment.GetEnvironmentVariable("PATITO_IDE_TOKEN_AUD")
            ?? _configuration["PatitoIde:TokenAudience"]
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

    private static long? GetNullableInt64(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null && value.TryGetInt64(out var result)
            ? result
            : null;
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
