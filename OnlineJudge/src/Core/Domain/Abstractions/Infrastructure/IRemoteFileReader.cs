namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IRemoteFileReader
{
    // Throws when the file cannot be downloaded.
    Task<(byte[] Bytes, string? MediaType)> ReadAsync(string url);
}
