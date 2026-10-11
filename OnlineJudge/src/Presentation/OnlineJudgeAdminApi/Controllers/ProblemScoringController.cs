using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/problems")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public sealed class ProblemScoringController : ControllerBase
{
    private readonly IProblemScoringService _scoring;
    private readonly IProblemReviewService _review;
    private readonly CurrentUser _currentUser;

    public ProblemScoringController(IProblemScoringService scoring, IProblemReviewService review, UserClaimsHelper claims)
    {
        _review = review ?? throw new ArgumentNullException(nameof(review));
        _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
        _currentUser = (claims ?? throw new ArgumentNullException(nameof(claims))).GetUserContextRole();
    }

    // The administrator reviews the problems of every site; teachers and assistants, those of their own.
    [HttpGet("review")]
    public async Task<IActionResult> ReviewAsync()
    {
        return Ok(await _review.ReviewAsync(_currentUser.Role == UserRolesEnum.Administrador ? null : _currentUser.SiteId));
    }

    [HttpGet("{problemId:int}/scoring")]
    public async Task<IActionResult> GetAsync(int problemId)
    {
        return Ok(await _scoring.GetAsync(problemId, _currentUser.SiteId));
    }

    [HttpPut("{problemId:int}/scoring")]
    public async Task<IActionResult> SaveAsync(int problemId, ProblemScoring scoring)
    {
        return Ok(await _scoring.SaveAsync(problemId, _currentUser.SiteId, scoring));
    }
}
