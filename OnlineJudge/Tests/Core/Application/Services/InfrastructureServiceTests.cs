using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;
using OnlineJudgeAdmin.Infrastructure.IdeIntegration.Implementations;

public class InfrastructureServiceTests
{
    [Fact]
    public void IdeLaunchTokenValidator_ValidatesSignedTokenAndReturnsClaims()
    {
        var secret = new string('s', 32);
        var validator = new IdeLaunchTokenValidator(CreateConfiguration(new Dictionary<string, string?>
        {
            ["PatitoIde:TokenSecret"] = secret
        }));
        var token = CreateToken(secret, new Dictionary<string, object?>
        {
            ["iss"] = "patito-online-judge",
            ["aud"] = "patito-ide",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            ["sub"] = "student1",
            ["site_id"] = 1,
            ["problem_id"] = 1000,
            ["contest_id"] = 3040,
            ["num"] = 0,
            ["allowed_languages"] = new[] { 2, 3 }
        });

        var claims = validator.Validate(token);

        Assert.Equal("student1", claims.UserId);
        Assert.Equal(1, claims.SiteId);
        Assert.Equal(1000, claims.ProblemId);
        Assert.Equal(3040, claims.ContestId);
        Assert.Equal(0, claims.Num);
        Assert.Equal(new[] { 2, 3 }, claims.AllowedLanguages);
    }

    [Fact]
    public void IdeLaunchTokenValidator_RejectsInvalidSignature()
    {
        var secret = new string('s', 32);
        var validator = new IdeLaunchTokenValidator(CreateConfiguration(new Dictionary<string, string?>
        {
            ["PatitoIde:TokenSecret"] = secret
        }));
        var token = CreateToken(secret, new Dictionary<string, object?>
        {
            ["iss"] = "patito-online-judge",
            ["aud"] = "patito-ide",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            ["sub"] = "student1"
        });

        var error = Assert.Throws<ArgumentException>(() => validator.Validate(token + "broken"));

        Assert.Equal("Invalid IDE launch token signature.", error.Message);
    }

    [Fact]
    public void IdeLaunchTokenValidator_RejectsExpiredToken()
    {
        var secret = new string('s', 32);
        var validator = new IdeLaunchTokenValidator(CreateConfiguration(new Dictionary<string, string?>
        {
            ["PatitoIde:TokenSecret"] = secret
        }));
        var token = CreateToken(secret, new Dictionary<string, object?>
        {
            ["iss"] = "patito-online-judge",
            ["aud"] = "patito-ide",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
            ["sub"] = "student1"
        });

        var error = Assert.Throws<ArgumentException>(() => validator.Validate(token));

        Assert.Equal("Expired IDE launch token.", error.Message);
    }

    [Fact]
    public void IdeLaunchTokenValidator_RequiresConfiguredSecret()
    {
        var validator = new IdeLaunchTokenValidator(CreateConfiguration(new Dictionary<string, string?>()));
        var token = "header.payload.signature";

        var error = Assert.Throws<InvalidOperationException>(() => validator.Validate(token));

        Assert.Equal("PATITO_IDE_TOKEN_SECRET must be configured with at least 32 characters.", error.Message);
    }

    [Fact]
    public void FileSystemLocalManager_CreatesFolderAndWritesFileUnderConfiguredPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "patito-tests", Guid.NewGuid().ToString("N"));
        var service = new FileSystemLocalManagerManager(CreateConfiguration(new Dictionary<string, string?>
        {
            ["FileSettings:ProblemsFilePath"] = root
        }));

        try
        {
            service.CreateFolder("1000");
            service.WriteToFile("1000", "sample.in", "1 2");

            Assert.True(Directory.Exists(Path.Combine(root, "1000")));
            Assert.Equal("1 2" + Environment.NewLine, File.ReadAllText(Path.Combine(root, "1000", "sample.in")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new Mock<IConfiguration>();
        foreach (var pair in values)
        {
            configuration.SetupGet(item => item[pair.Key]).Returns(pair.Value);
        }

        return configuration.Object;
    }

    private static string CreateToken(string secret, IReadOnlyDictionary<string, object?> payload)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        })));
        var body = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var unsignedToken = $"{header}.{body}";
        var signature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(unsignedToken)));

        return $"{unsignedToken}.{signature}";
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
