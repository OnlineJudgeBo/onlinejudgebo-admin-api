using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Machines panel of an exam. The rules live in MachinesPanelService.
[ApiController]
[Route("/api/contests/{contestId:int}/machines")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public class ContestMachinesController : ControllerBase
{
    private readonly IContestMachinesService _machinesService;
    private readonly IMachinesPanelService _panelService;
    private readonly CurrentUser _currentUser;

    public ContestMachinesController(IContestMachinesService machinesService, IMachinesPanelService panelService, UserClaimsHelper userClaimsHelper)
    {
        _machinesService = machinesService ?? throw new ArgumentNullException(nameof(machinesService));
        _panelService = panelService ?? throw new ArgumentNullException(nameof(panelService));
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
    [RequestSizeLimit(262_144)]
    public async Task<IActionResult> ForwardAsync(int contestId, string? path)
    {
        var body = HttpMethods.IsGet(Request.Method) ? null : await new StreamReader(Request.Body).ReadToEndAsync();
        return Send(await _panelService.ForwardAsync(contestId, _currentUser.SiteId, Request.Method, path, Request.QueryString.Value ?? string.Empty, body, HttpContext.RequestAborted));
    }

    // The lobby panel: machines that booted the ISO but are in no exam.
    [HttpGet("/api/lobby/machines/group")]
    public async Task<IActionResult> GetLobbyGroupAsync()
    {
        var group = await _machinesService.GetLobbyGroupAsync(_currentUser.SiteId);
        return Ok(new { groupId = group.Id, label = group.Label });
    }

    [HttpGet("/api/lobby/machines/{**path}")]
    [HttpPost("/api/lobby/machines/{**path}")]
    [HttpPut("/api/lobby/machines/{**path}")]
    [RequestSizeLimit(262_144)]
    public async Task<IActionResult> ForwardLobbyAsync(string? path)
    {
        var body = HttpMethods.IsGet(Request.Method) ? null : await new StreamReader(Request.Body).ReadToEndAsync();
        return Send(await _panelService.ForwardLobbyAsync(_currentUser.SiteId, Request.Method, path, Request.QueryString.Value ?? string.Empty, body, HttpContext.RequestAborted));
    }

    private IActionResult Send(MachinesForwardResult result)
    {
        if (result.Response is not { } response)
        {
            return result.Message == null ? NotFound() : StatusCode(result.StatusCode, new { message = result.Message });
        }

        HttpContext.Response.RegisterForDispose(response);
        Response.StatusCode = response.StatusCode;
        if (response.ContentDisposition != null)
        {
            Response.Headers.ContentDisposition = response.ContentDisposition;
        }

        return new FileStreamResult(response.Content, response.ContentType ?? "application/octet-stream");
    }
}
