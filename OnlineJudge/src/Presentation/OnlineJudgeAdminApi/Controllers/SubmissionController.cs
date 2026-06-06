using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdminApi.Services.IdeIntegration;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class SubmissionController : ControllerBase
{
    private readonly IAcademicService _academicService;
    private readonly IIdeSubmissionService _ideSubmissionService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public SubmissionController(
        IAcademicService academicService,
        IIdeSubmissionService ideSubmissionService,
        UserClaimsHelper userClaimsHelper)
    {
        _academicService = academicService ?? throw new ArgumentNullException(nameof(academicService));
        _ideSubmissionService = ideSubmissionService ?? throw new ArgumentNullException(nameof(ideSubmissionService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpPost]
    public async Task<IActionResult> SubmitAsync(SubmissionForCreation submission)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();

        var request = new AcademicSubmissionRequest
        {
            ProblemId = submission.ProblemId,
            SourceCode = submission.SourceCode,
            LanguageId = submission.LanguageId,
            ContestId = submission.ContestId,
            CourseId = submission.CourseId,
            AssignmentId = submission.AssignmentId,
            FileName = submission.FileName
        };

        return Ok(await _academicService.SubmitAsync(currentUser, request));
    }

    [AllowAnonymous]
    [HttpPost("/api/vibe/submissions")]
    public async Task<IActionResult> SubmitFromIdeAsync([FromBody] VibeSubmissionForCreation submission)
    {
        return Ok(await _ideSubmissionService.SubmitAsync(Request.GetBearerToken(), submission));
    }

    [AllowAnonymous]
    [HttpGet("/api/vibe/submissions/{submissionId:int}")]
    public async Task<IActionResult> GetIdeSubmissionStatusAsync(int submissionId)
    {
        return Ok(await _ideSubmissionService.GetStatusAsync(Request.GetBearerToken(), submissionId));
    }

    [AllowAnonymous]
    [HttpPost("/api/vibe/runs")]
    public async Task<IActionResult> RunFromIdeAsync([FromBody] VibeSubmissionForCreation submission)
    {
        return Ok(await _ideSubmissionService.CustomInputAsync(Request.GetBearerToken(), submission));
    }

    [AllowAnonymous]
    [HttpPost("/api/vibe/custom-input")]
    public async Task<IActionResult> CustomInputFromIdeAsync([FromBody] VibeSubmissionForCreation submission)
    {
        return Ok(await _ideSubmissionService.CustomInputAsync(Request.GetBearerToken(), submission));
    }

    [AllowAnonymous]
    [HttpGet("/api/vibe/runs/{runId:int}")]
    public async Task<IActionResult> GetIdeRunStatusAsync(int runId)
    {
        return Ok(await _ideSubmissionService.GetStatusAsync(Request.GetBearerToken(), runId));
    }
}
