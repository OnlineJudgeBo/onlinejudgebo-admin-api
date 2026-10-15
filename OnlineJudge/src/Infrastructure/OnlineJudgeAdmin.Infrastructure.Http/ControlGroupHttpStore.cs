using System.Net.Http.Headers;
using System.Text.Json;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace OnlineJudgeAdmin.Infrastructure.Http;

// control-server owns groups.json; Patito only asks it to provision an exam group or the lobby.
public sealed class ControlGroupHttpStore(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IControlGroupStore
{
    public async Task EnsureAsync(ControlGroup group)
    {
        var client = httpClientFactory.CreateClient(ControlServerHttpClient.ClientName);
        if (client.BaseAddress == null)
        {
            throw new InvalidOperationException("ControlServer:Url is not configured.");
        }

        var adminToken = configuration["ControlServer:AdminToken"];
        if (string.IsNullOrWhiteSpace(adminToken))
        {
            throw new InvalidOperationException("ControlServer:AdminToken is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, $"admin/groups/{Uri.EscapeDataString(group.Id)}")
        {
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(new
            {
                enroll_token = group.EnrollToken,
                admin_token = group.AdminToken,
                label = group.Label,
            })),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException("No se pudo conectar con el control de máquinas.", error);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                if (detail.Length > 512)
                {
                    detail = detail[..512];
                }

                throw new InvalidOperationException(
                    $"El control de máquinas rechazó el grupo '{group.Id}' ({(int)response.StatusCode}): {detail}");
            }
        }
    }
}
