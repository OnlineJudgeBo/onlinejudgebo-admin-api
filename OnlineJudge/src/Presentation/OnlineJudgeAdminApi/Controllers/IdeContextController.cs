using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdminApi.Services.IdeIntegration;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/ide")]
[AllowAnonymous]
public sealed class IdeContextController : ControllerBase
{
    private readonly IIdeLaunchTokenValidator _tokenValidator;
    private readonly IIdeContextService _contextService;

    public IdeContextController(
        IIdeLaunchTokenValidator tokenValidator,
        IIdeContextService contextService)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
        _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
    }

    [HttpGet("context")]
    [HttpGet("/api/vibe/context")]
    public async Task<IActionResult> GetContextAsync(
        [FromQuery] int? problemId,
        [FromQuery] int? languageId,
        [FromQuery] string? languageName,
        [FromQuery] string? handoff,
        [FromQuery] string? token)
    {
        IdeLaunchClaims claims;
        try
        {
            claims = _tokenValidator.Validate(token ?? Request.GetBearerToken());
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return Unauthorized(new { error = "invalid_ide_token" });
        }

        if (claims.ProblemId <= 0)
        {
            return BadRequest(new { error = "problem_id claim is required." });
        }

        if (problemId.HasValue && problemId.Value > 0 && problemId.Value != claims.ProblemId)
        {
            return Forbid();
        }

        var result = await _contextService.BuildContextAsync(
            claims,
            new IdeContextRequest(problemId, languageId, languageName, handoff));

        return result.Status switch
        {
            IdeContextBuildStatus.Success => Ok(result.Response),
            IdeContextBuildStatus.UserNotAllowed => Forbid(),
            IdeContextBuildStatus.ProblemNotFound => NotFound(new { error = $"Problem {claims.ProblemId} was not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

}
