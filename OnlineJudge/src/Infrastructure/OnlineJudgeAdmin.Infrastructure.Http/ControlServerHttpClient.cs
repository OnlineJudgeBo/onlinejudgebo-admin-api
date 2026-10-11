using System.Net.Http.Headers;
using System.Text;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Http;

public sealed class ControlServerHttpClient(IHttpClientFactory httpClientFactory) : IControlServerClient
{
    public const string ClientName = "control-server";

    public bool IsConfigured => httpClientFactory.CreateClient(ClientName).BaseAddress != null;

    public async Task<ControlServerResponse?> SendAsync(string method, string pathAndQuery, string token, string? body = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(new HttpMethod(method), pathAndQuery);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
        {
            message.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(ClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        return new ControlServerResponse(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            response.Content.Headers.ContentDisposition?.ToString(),
            await response.Content.ReadAsStreamAsync(cancellationToken),
            response);
    }
}
