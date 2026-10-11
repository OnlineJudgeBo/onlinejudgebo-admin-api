using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/problems")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public sealed class ProblemScoringController : ControllerBase
{
    private readonly ProblemScoringService _scoring;
    private readonly CurrentUser _currentUser;

    public ProblemScoringController(ProblemScoringService scoring, UserClaimsHelper claims)
    {
        _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
        _currentUser = (claims ?? throw new ArgumentNullException(nameof(claims))).GetUserContextRole();
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
