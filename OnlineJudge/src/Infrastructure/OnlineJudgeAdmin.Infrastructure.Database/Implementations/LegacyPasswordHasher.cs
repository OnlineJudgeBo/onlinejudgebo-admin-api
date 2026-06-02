using System.Security.Cryptography;
using System.Text;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

internal static class LegacyPasswordHasher
{
    public static bool Verify(string password, string? savedHash)
    {
        if (string.IsNullOrWhiteSpace(savedHash))
        {
            return false;
        }

        if (savedHash.All(static c => Uri.IsHexDigit(c)))
        {
            return string.Equals(ComputeMd5Hex(password), savedHash, StringComparison.OrdinalIgnoreCase);
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(savedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length < 24)
        {
            return false;
        }

        var salt = Encoding.UTF8.GetString(decoded[20..]);
        var expectedDigest = ComputeSha1Bytes(ComputeMd5Hex(password) + salt);
        var expectedSaltBytes = Encoding.UTF8.GetBytes(salt);
        var expected = new byte[expectedDigest.Length + expectedSaltBytes.Length];

        Buffer.BlockCopy(expectedDigest, 0, expected, 0, expectedDigest.Length);
        Buffer.BlockCopy(expectedSaltBytes, 0, expected, expectedDigest.Length, expectedSaltBytes.Length);

        return expected.Length == decoded.Length && CryptographicOperations.FixedTimeEquals(expected, decoded);
    }

    private static string ComputeMd5Hex(string value)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static byte[] ComputeSha1Bytes(string value)
    {
        using var sha1 = SHA1.Create();
        return sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
    }
}
