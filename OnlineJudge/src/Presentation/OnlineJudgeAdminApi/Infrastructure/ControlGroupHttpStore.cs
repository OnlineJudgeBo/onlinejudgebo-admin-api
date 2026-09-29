using System.Net.Http.Headers;
using System.Text.Json;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;

namespace OnlineJudgeAdminApi.Infrastructure;

// control-server owns groups.json; Patito only asks it to provision an exam group.
public sealed class ControlGroupHttpStore(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IControlGroupStore
{
    public async Task EnsureAsync(ControlGroup group)
    {
        var client = httpClientFactory.CreateClient(ContestMachinesController.ControlServerClient);
        if (client.BaseAddress == null)
        {
            throw new InvalidOperationException("ControlServer:Url is not configured.");
        }

        var adminToken = configuration["ControlServer:AdminToken"];
        if (string.IsNullOrWhiteSpace(adminToken))
        {
            throw new InvalidOperationException("ControlServer:AdminToken is not configured.");
        }

        await PutAsync(client, adminToken, group.Id, group.Label, group.EnrollToken, group.AdminToken);

        var lobbyToken = configuration["ControlServer:LobbyEnrollToken"];
        if (!string.IsNullOrWhiteSpace(lobbyToken))
        {
            await PutAsync(client, adminToken, "lobby", "Sin examen", lobbyToken, null);
        }
    }

    private static async Task PutAsync(HttpClient client, string token, string id, string label, string enrollToken, string? groupAdminToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"admin/groups/{Uri.EscapeDataString(id)}")
        {
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(new
            {
                enroll_token = enrollToken,
                admin_token = groupAdminToken,
                label,
            })),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
                    $"El control de máquinas rechazó el grupo '{id}' ({(int)response.StatusCode}): {detail}");
            }
        }
    }
}
