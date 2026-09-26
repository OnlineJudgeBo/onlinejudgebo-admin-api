using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Machines panel of an exam: forwards to the control-server's /admin/* with the exam group's own
// admin token, so the control-server itself limits every call to that contest's machines.
[ApiController]
[Route("/api/contests/{contestId:int}/machines")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public class ContestMachinesController : ControllerBase
{
    public const string ControlServerClient = "control-server";

    // Team credentials stay out of Patito (students log in with their Patito account); SSE is replaced by polling.
    private static readonly HashSet<string> BlockedSections = new(StringComparer.OrdinalIgnoreCase) { "credentials", "events" };

    private readonly IContestMachinesService _machinesService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CurrentUser _currentUser;

    public ContestMachinesController(IContestMachinesService machinesService, IHttpClientFactory httpClientFactory, UserClaimsHelper userClaimsHelper)
    {
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

        using var message = new HttpRequestMessage(new HttpMethod(Request.Method), $"admin/{string.Join('/', segments)}{Request.QueryString}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", group.AdminToken);
        if (!HttpMethods.IsGet(Request.Method))
        {
            message.Content = new StreamContent(Request.Body);
            if (Request.ContentType != null)
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(Request.ContentType);
            }
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
}
