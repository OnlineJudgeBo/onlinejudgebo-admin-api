using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/patito-ide")]
[Authorize]
public sealed class IdeLaunchTokenController : ControllerBase
{
    private readonly IIdeLaunchTokenIssuer _tokenIssuer;
    private readonly UserClaimsHelper _userClaimsHelper;

    public IdeLaunchTokenController(IIdeLaunchTokenIssuer tokenIssuer, UserClaimsHelper userClaimsHelper)
    {
        _tokenIssuer = tokenIssuer ?? throw new ArgumentNullException(nameof(tokenIssuer));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpPost("launch-token")]
    public IActionResult CreateLaunchToken([FromBody] IdeLaunchTokenForCreation request)
    {
        if (request.ProblemId <= 0)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = "problemId is required." });
        }

        var currentUser = _userClaimsHelper.GetUserContextRole();

        var claims = new IdeLaunchClaims(
            currentUser.UserId,
            currentUser.SiteId,
            request.ProblemId,
            request.ContestId is > 0 ? request.ContestId : null,
            request.Num,
            (request.AllowedLanguages ?? Array.Empty<int>()).Where(languageId => languageId > 0).ToArray());

        var token = _tokenIssuer.Issue(claims);
        return Ok(new IdeLaunchTokenResponse { Token = token });
    }
}
