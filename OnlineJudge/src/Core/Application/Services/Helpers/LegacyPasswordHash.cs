using System.Security.Cryptography;
using System.Text;

namespace OnlineJudgeAdmin.Core.Application.Services.Helpers;

internal static class LegacyPasswordHash
{
    public static string Generate(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var md5Password = ComputeMd5Hex(password);
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant();
        var digest = ComputeSha1Bytes(md5Password + salt);
        var saltBytes = Encoding.UTF8.GetBytes(salt);
        var combined = new byte[digest.Length + saltBytes.Length];

        Buffer.BlockCopy(digest, 0, combined, 0, digest.Length);
        Buffer.BlockCopy(saltBytes, 0, combined, digest.Length, saltBytes.Length);

        return Convert.ToBase64String(combined);
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
