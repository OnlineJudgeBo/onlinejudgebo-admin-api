using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Staff side of contest clarifications; participants use /api/public/contests/{id}/clarifications.
[ApiController]
[Route("/api/contests")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public class ContestClarificationsController : ControllerBase
{
    private readonly IContestClarificationService _clarificationService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public ContestClarificationsController(IContestClarificationService clarificationService, UserClaimsHelper userClaimsHelper)
    {
        _clarificationService = clarificationService ?? throw new ArgumentNullException(nameof(clarificationService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpGet("clarifications/pending")]
    public async Task<IActionResult> GetPendingCountsAsync()
    {
        return Ok(await _clarificationService.GetPendingCountsAsync(_userClaimsHelper.GetUserContextRole().SiteId));
    }

    [HttpGet("{contestId:int}/clarifications")]
    public async Task<IActionResult> GetAllAsync(int contestId)
    {
        return Ok(await _clarificationService.GetAllAsync(contestId, _userClaimsHelper.GetUserContextRole().SiteId));
    }

    [HttpPut("{contestId:int}/clarifications/{id:int}/answer")]
    public async Task<IActionResult> AnswerAsync(int contestId, int id, ClarificationAnswerRequest request)
    {
        return Ok(await _clarificationService.AnswerAsync(contestId, id, request.Answer, request.IsPublic, _userClaimsHelper.GetUserContextRole()));
    }

    [HttpPost("{contestId:int}/clarifications/announcements")]
    public async Task<IActionResult> AnnounceAsync(int contestId, ClarificationAnnouncementRequest request)
    {
        return Ok(await _clarificationService.AnnounceAsync(contestId, request.ProblemNum, request.Text, _userClaimsHelper.GetUserContextRole()));
    }
}
