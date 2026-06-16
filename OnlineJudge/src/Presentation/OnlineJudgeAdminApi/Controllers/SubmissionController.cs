using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

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
    [HttpPost("/api/patito-ide/submissions")]
    public async Task<IActionResult> SubmitFromIdeAsync([FromBody] VibeSubmissionForCreation submission)
    {
        return Ok(ToVibeSubmissionResponse(await _ideSubmissionService.SubmitAsync(Request.GetBearerToken(), ToIdeSubmissionRequest(submission))));
    }

    [AllowAnonymous]
    [HttpGet("/api/patito-ide/submissions/{submissionId:int}")]
    public async Task<IActionResult> GetIdeSubmissionStatusAsync(int submissionId)
    {
        return Ok(ToVibeSubmissionStatusResponse(await _ideSubmissionService.GetStatusAsync(Request.GetBearerToken(), submissionId)));
    }

    [AllowAnonymous]
    [HttpPost("/api/patito-ide/runs")]
    public async Task<IActionResult> RunFromIdeAsync([FromBody] VibeSubmissionForCreation submission)
    {
        return Ok(ToVibeRunResponse(await _ideSubmissionService.CustomInputAsync(Request.GetBearerToken(), ToIdeSubmissionRequest(submission))));
    }

    [AllowAnonymous]
    [HttpPost("/api/patito-ide/custom-input")]
    public async Task<IActionResult> CustomInputFromIdeAsync([FromBody] VibeSubmissionForCreation submission)
    {
        return Ok(ToVibeRunResponse(await _ideSubmissionService.CustomInputAsync(Request.GetBearerToken(), ToIdeSubmissionRequest(submission))));
    }

    [AllowAnonymous]
    [HttpGet("/api/patito-ide/runs/{runId:int}")]
    public async Task<IActionResult> GetIdeRunStatusAsync(int runId)
    {
        return Ok(ToVibeSubmissionStatusResponse(await _ideSubmissionService.GetStatusAsync(Request.GetBearerToken(), runId)));
    }

    private static IdeSubmissionRequest ToIdeSubmissionRequest(VibeSubmissionForCreation submission)
    {
        return new IdeSubmissionRequest
        {
            SourceCode = submission.SourceCode,
            LanguageId = submission.LanguageId,
            ProblemId = submission.ProblemId,
            ContestId = submission.ContestId,
            Num = submission.Num,
            Stdin = submission.Stdin,
            Testcases = submission.Testcases
                .Select(item => new IdeTestcaseRequest
                {
                    Input = item.Input,
                    ExpectedOutput = item.ExpectedOutput
                })
                .ToArray()
        };
    }

    private static VibeSubmissionResponse ToVibeSubmissionResponse(IdeSubmissionResponse response)
    {
        return new VibeSubmissionResponse
        {
            SubmissionId = response.SubmissionId,
            Id = response.Id,
            StatusUrl = response.StatusUrl
        };
    }

    private static VibeRunResponse ToVibeRunResponse(IdeRunResponse response)
    {
        return new VibeRunResponse
        {
            RunId = response.RunId,
            Id = response.Id,
            StatusUrl = response.StatusUrl
        };
    }

    private static VibeSubmissionStatusResponse ToVibeSubmissionStatusResponse(IdeSubmissionStatusResponse response)
    {
        return new VibeSubmissionStatusResponse
        {
            SubmissionId = response.SubmissionId,
            Id = response.Id,
            Phase = response.Phase,
            Verdict = response.Verdict,
            Stdout = response.Stdout,
            Stderr = response.Stderr,
            CompileErrors = response.CompileErrors,
            Logs = response.Logs,
            RuntimeMs = response.RuntimeMs,
            MemoryKb = response.MemoryKb
        };
    }
}
