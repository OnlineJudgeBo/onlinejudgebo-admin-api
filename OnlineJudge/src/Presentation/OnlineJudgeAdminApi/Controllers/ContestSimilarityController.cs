using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/contests")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public sealed class ContestSimilarityController : ControllerBase
{
    private readonly IContestSimilarityService _similarity;
    private readonly CurrentUser _currentUser;

    public ContestSimilarityController(IContestSimilarityService similarity, UserClaimsHelper claims)
    {
        _similarity = similarity ?? throw new ArgumentNullException(nameof(similarity));
        _currentUser = (claims ?? throw new ArgumentNullException(nameof(claims))).GetUserContextRole();
    }

    [HttpGet("{contestId:int}/similarity")]
    public async Task<IActionResult> GetAsync(int contestId)
    {
        return Ok(await _similarity.GetAsync(contestId, _currentUser.SiteId));
    }

    [HttpPost("{contestId:int}/similarity/run")]
    public async Task<IActionResult> RunAsync(int contestId)
    {
        await _similarity.RequestRunAsync(contestId, _currentUser.SiteId);
        return Accepted();
    }

    [HttpGet("{contestId:int}/submissions/export")]
    public async Task<IActionResult> ExportSubmissionsAsync(int contestId)
    {
        try
        {
            var bytes = await _similarity.ExportSubmissionsAsync(contestId, _currentUser.SiteId);
            return new FileContentResult(bytes, "application/zip") { FileDownloadName = $"contest-{contestId}-solutions.zip" };
        }
        catch (KeyNotFoundException error)
        {
            return new NotFoundObjectResult(new ErrorDetails { StatusCode = 404, Message = error.Message });
        }
    }
}
