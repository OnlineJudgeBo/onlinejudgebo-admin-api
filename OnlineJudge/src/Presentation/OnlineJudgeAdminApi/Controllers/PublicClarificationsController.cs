using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Participant side of contest clarifications: announcements and public answers, plus the caller's own questions.
[ApiController]
[Route("/api/public/contests/{contestId:int}/clarifications")]
public class PublicClarificationsController : ControllerBase
{
    private readonly IContestClarificationService _clarificationService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public PublicClarificationsController(IContestClarificationService clarificationService, UserClaimsHelper userClaimsHelper)
    {
        _clarificationService = clarificationService ?? throw new ArgumentNullException(nameof(clarificationService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAsync(int contestId, [FromQuery] int siteId = 1)
    {
        var currentUser = _userClaimsHelper.TryGetUserContextRole();
        var effectiveSiteId = currentUser?.SiteId > 0 ? currentUser.SiteId : siteId;
        return Ok(await _clarificationService.GetForParticipantAsync(effectiveSiteId, contestId, currentUser));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AskAsync(int contestId, ClarificationQuestionRequest request, [FromQuery] int siteId = 1)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var effectiveSiteId = currentUser.SiteId > 0 ? currentUser.SiteId : siteId;
        return Ok(await _clarificationService.AskAsync(effectiveSiteId, contestId, request.ProblemNum, request.Question, currentUser));
    }
}
