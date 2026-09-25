using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnlineJudgeAdmin.Infrastructure.IdeIntegration.Implementations;

public sealed class IdeLaunchTokenIssuer : IIdeLaunchTokenIssuer
{
    private const string DefaultIssuer = "patito-online-judge";
    private const string DefaultAudience = "patito-ide";
    private const int DefaultTtlSeconds = 7200;
    private static readonly string EncodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));

    private readonly IConfiguration _configuration;

    public IdeLaunchTokenIssuer(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string Issue(IdeLaunchClaims claims)
    {
        if (string.IsNullOrWhiteSpace(claims.UserId))
        {
            throw new ArgumentException("UserId es requerido para emitir un token de lanzamiento de IDE.");
        }

        if (claims.ProblemId <= 0)
        {
            throw new ArgumentException("ProblemId es requerido para emitir un token de lanzamiento de IDE.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = TokenIssuer(),
            ["aud"] = TokenAudience(),
            ["sub"] = claims.UserId,
            ["site_id"] = claims.SiteId,
            ["problem_id"] = claims.ProblemId,
            ["contest_id"] = claims.ContestId,
            ["num"] = claims.Num,
            ["course_id"] = claims.CourseId,
            ["assignment_id"] = claims.AssignmentId,
            ["allowed_languages"] = claims.AllowedLanguages,
            ["exp"] = DateTimeOffset.UtcNow.Add(TokenTtl()).ToUnixTimeSeconds(),
        };

        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var unsignedToken = $"{EncodedHeader}.{payloadPart}";
        var signature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TokenSecret()), Encoding.UTF8.GetBytes(unsignedToken)));

        return $"{unsignedToken}.{signature}";
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

    private TimeSpan TokenTtl()
    {
        var raw = Environment.GetEnvironmentVariable("PATITO_IDE_TOKEN_TTL_SECONDS")
            ?? _configuration["PatitoIde:TokenTtlSeconds"];
        return int.TryParse(raw, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(DefaultTtlSeconds);
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
