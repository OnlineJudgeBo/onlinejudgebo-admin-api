using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Machines panel of an exam: forwards to the control-server's /admin/* with the exam group's own
// admin token, so the control-server itself limits the call to that contest's machines. The few
// superadmin-only operations use the superadmin token and are pinned to the group here.
[ApiController]
[Route("/api/contests/{contestId:int}/machines")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public class ContestMachinesController : ControllerBase
{
    public const string ControlServerClient = "control-server";

    // Team credentials stay out of Patito (students log in with their Patito account); SSE is replaced by polling.
    private static readonly HashSet<string> BlockedSections = new(StringComparer.OrdinalIgnoreCase) { "credentials", "events" };

    // Mirror of SUPERADMIN_ONLY in control-server/server.py.
    private static readonly HashSet<string> SuperadminActions = new() { "unlock-root", "lock-root", "net-open", "usb-block", "usb-unblock", "collect-home", "set-allowlist" };

    private readonly IContestMachinesService _machinesService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly CurrentUser _currentUser;

    public ContestMachinesController(IContestMachinesService machinesService, IHttpClientFactory httpClientFactory, IConfiguration configuration, UserClaimsHelper userClaimsHelper)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _machinesService = machinesService ?? throw new ArgumentNullException(nameof(machinesService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _currentUser = (userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper))).GetUserContextRole();
    }

    [HttpGet("group")]
    public async Task<IActionResult> GetGroupAsync(int contestId)
    {
        var group = await _machinesService.GetGroupAsync(contestId, _currentUser.SiteId);
        return Ok(new { groupId = group.Id, label = group.Label });
    }

    [HttpGet("{**path}")]
    [HttpPost("{**path}")]
    [HttpPut("{**path}")]
    public async Task<IActionResult> ForwardAsync(int contestId, string? path)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || BlockedSections.Contains(segments[0]) || segments.Any(segment => segment is "." or ".."))
        {
            return NotFound();
        }

        var group = await _machinesService.GetGroupAsync(contestId, _currentUser.SiteId);
        var client = _httpClientFactory.CreateClient(ControlServerClient);
        if (client.BaseAddress == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "El control de máquinas no está configurado." });
        }

        var query = Request.QueryString.Value ?? string.Empty;
        var body = HttpMethods.IsGet(Request.Method) ? null : await new StreamReader(Request.Body).ReadToEndAsync();
        var token = group.AdminToken;

        // The control-server only allows these to its superadmin token, which is not limited to one group,
        // so here Patito pins them to this exam's group itself.
        var superadmin = segments[0] == "allowlist"
            || (segments[0] == "cmd" && SuperadminActions.Contains(JsonField(body, "action") ?? string.Empty));
        if (superadmin)
        {
            token = _configuration["ControlServer:AdminToken"] ?? string.Empty;
            if (token.Length == 0)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Esta acción requiere ControlServer:AdminToken." });
            }

            if (segments[0] == "allowlist")
            {
                query = $"?group={Uri.EscapeDataString(group.Id)}";
                body = body == null ? null : WithGroup(body, group.Id);
            }
            else if (!await TargetsOnlyGroupAsync(client, group, body!))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Solo se pueden enviar órdenes a las máquinas de este examen." });
            }
        }

        using var message = new HttpRequestMessage(new HttpMethod(Request.Method), $"admin/{string.Join('/', segments)}{query}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
        {
            message.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "No se pudo conectar con el control de máquinas." });
        }

        HttpContext.Response.RegisterForDispose(response);
        Response.StatusCode = (int)response.StatusCode;
        if (response.Content.Headers.ContentDisposition != null)
        {
            Response.Headers.ContentDisposition = response.Content.Headers.ContentDisposition.ToString();
        }

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return new FileStreamResult(await response.Content.ReadAsStreamAsync(HttpContext.RequestAborted), contentType);
    }

    // A superadmin command may target this exam's group or its machines, never "all" or another group.
    private static async Task<bool> TargetsOnlyGroupAsync(HttpClient client, ControlGroup group, string body)
    {
        if (JsonNode.Parse(body)?["target"] is not JsonObject target || target.ContainsKey("all"))
        {
            return false;
        }

        var groupId = target["group_id"]?.GetValue<string>();
        var machineId = target["machine_id"]?.GetValue<string>();
        if (groupId == null && machineId == null)
        {
            return false;
        }

        if (groupId != null && groupId != group.Id)
        {
            return false;
        }

        if (machineId == null)
        {
            return true;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "admin/machines");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", group.AdminToken);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var machines = JsonNode.Parse(await response.Content.ReadAsStringAsync())?["machines"]?.AsArray() ?? new JsonArray();
        return machines.Any(machine => machine?["machine_id"]?.GetValue<string>() == machineId);
    }

    private static string? JsonField(string? body, string field)
    {
        try
        {
            return body == null ? null : (JsonNode.Parse(body) as JsonObject)?[field]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string WithGroup(string body, string groupId)
    {
        var json = JsonNode.Parse(body) as JsonObject ?? new JsonObject();
        json["group_id"] = groupId;
        return json.ToJsonString();
    }
}
