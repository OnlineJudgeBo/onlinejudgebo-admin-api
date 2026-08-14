using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/patito-ide/launch-token")]
[Authorize]
public sealed class IdeLaunchTokenController : ControllerBase
{
    private readonly IIdeLaunchTokenValidator _tokenIssuer;
    private readonly UserClaimsHelper _userClaimsHelper;

    public IdeLaunchTokenController(IIdeLaunchTokenValidator tokenIssuer, UserClaimsHelper userClaimsHelper)
    {
        _tokenIssuer = tokenIssuer ?? throw new ArgumentNullException(nameof(tokenIssuer));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpPost]
    public IActionResult CreateLaunchToken(IdeLaunchTokenForCreation request)
    {
        if (request.ProblemId <= 0)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = "problemId is required." });
        }

        var currentUser = _userClaimsHelper.GetUserContextRole();
        var token = _tokenIssuer.Issue(new IdeLaunchClaims(
            currentUser.UserId,
            currentUser.SiteId,
            request.ProblemId,
            request.ContestId,
            request.Num,
            request.AllowedLanguages ?? Array.Empty<int>()));

        return Ok(new { token });
    }
}
