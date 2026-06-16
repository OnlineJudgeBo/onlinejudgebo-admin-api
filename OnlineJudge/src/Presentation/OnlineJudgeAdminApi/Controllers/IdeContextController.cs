using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/patito-ide")]
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
            IdeContextBuildStatus.Success => Ok(ToIdeContextResponse(result.Response!)),
            IdeContextBuildStatus.UserNotAllowed => Forbid(),
            IdeContextBuildStatus.ProblemNotFound => NotFound(new { error = $"Problem {claims.ProblemId} was not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }


    private static OnlineJudgeAdminApi.DataTransferObjects.IdeContextResponse ToIdeContextResponse(OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration.IdeContextResponse response)
    {
        return new OnlineJudgeAdminApi.DataTransferObjects.IdeContextResponse(
            new IdeProblemContextDto(
                response.Problem.ProblemId,
                response.Problem.Title,
                response.Problem.Description,
                response.Problem.Input,
                response.Problem.Output,
                response.Problem.Constraints,
                response.Problem.Hints,
                new IdeProblemExampleDto(response.Problem.Example.Input, response.Problem.Example.Output),
                response.Problem.TimeLimit,
                response.Problem.MemoryLimit),
            response.AllowedLanguages,
            response.LanguageDefinitions
                .Select(item => new IdeLanguageDefinitionDto(item.JudgeLanguageId, item.Name, item.IdeLanguage))
                .ToArray(),
            new IdeContextIdentifiersDto(
                response.Identifiers.ProblemId,
                response.Identifiers.ContestId,
                response.Identifiers.Num,
                response.Identifiers.UserId,
                response.Identifiers.SiteId,
                response.Identifiers.LanguageId,
                response.Identifiers.LanguageName,
                response.Identifiers.Handoff));
    }
}
