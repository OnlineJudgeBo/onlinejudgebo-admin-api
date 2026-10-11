using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.Http;

public class HttpRemoteFileReader : IRemoteFileReader
{
    private static readonly HttpClient HttpClient = new();

    public async Task<(byte[] Bytes, string? MediaType)> ReadAsync(string url)
    {
        using var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.MediaType);
    }
}
