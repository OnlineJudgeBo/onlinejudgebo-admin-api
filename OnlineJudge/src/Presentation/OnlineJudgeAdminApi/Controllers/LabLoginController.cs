using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Login service the lab ISO calls (AUTH_SERVICE_URL). Answers the same contract as the
// control-server's auth-server.py, but with Patito accounts: the region is the exam group.
[ApiController]
[Route("/api/lab")]
[AllowAnonymous]
public partial class LabLoginController : ControllerBase
{
    private readonly IContestMachinesService _machinesService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public LabLoginController(IContestMachinesService machinesService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _machinesService = machinesService ?? throw new ArgumentNullException(nameof(machinesService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LabLoginRequest request)
    {
        var result = await _machinesService.LoginAsync(request.Username?.Trim() ?? string.Empty, request.Password ?? string.Empty, ClientIpHelper.GetClientIp(HttpContext));
        // The ISO reads "ok" and "message" from a 200 response; any other status is shown as an HTTP error.
        if (!result.Ok || result.Group == null)
        {
            return Ok(new { ok = false, message = result.Message });
        }

        var baseUrl = (_configuration["Base:Url"] ?? string.Empty).TrimEnd('/');
        var homepage = await GroupSettingAsync(result.Group, "homepage", "url")
            ?? (baseUrl.Length > 0 ? $"{baseUrl}/oj/contest.php?cid={result.ContestId}" : string.Empty);
        var logoUrl = await GroupSettingAsync(result.Group, "logo", "effective_url") ?? string.Empty;

        return Ok(new
        {
            ok = true,
            userId = result.UserId,
            displayName = result.DisplayName,
            homepage,
            logoUrl,
            team = new { id = TeamId(result.UserId), name = result.DisplayName },
            region = new { id = result.Group.Id, name = result.Group.Label, enrollToken = result.Group.EnrollToken },
        });
    }

    // Homepage and logo set for this exam in the machines panel; null when unset or the control-server is down.
    private async Task<string?> GroupSettingAsync(ControlGroup group, string setting, string field)
    {
        var client = _httpClientFactory.CreateClient(ContestMachinesController.ControlServerClient);
        if (client.BaseAddress == null)
        {
            return null;
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"admin/{setting}?group={Uri.EscapeDataString(group.Id)}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", group.AdminToken);
            using var response = await client.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var value = json.RootElement.TryGetProperty(field, out var property) ? property.GetString() : null;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // The ISO only accepts team ids made of [A-Za-z0-9._-] without "..".
    private static string TeamId(string userId)
    {
        var id = UnsafeTeamIdChars().Replace(userId, "-").Replace("..", "-");
        return id.Length == 0 ? "equipo" : id[..Math.Min(id.Length, 64)];
    }

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeTeamIdChars();
}
