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
            FileName = submission.FileName,
            ClientIp = ClientIpHelper.GetClientIp(HttpContext)
        };

        return Ok(await _academicService.SubmitAsync(currentUser, request));
    }

    [AllowAnonymous]
    [HttpPost("/api/patito-ide/submissions")]
    public async Task<IActionResult> SubmitFromIdeAsync([FromBody] PatitoIdeSubmissionForCreation submission)
    {
        return Ok(ToPatitoIdeSubmissionResponse(await _ideSubmissionService.SubmitAsync(Request.GetBearerToken(), ToIdeSubmissionRequest(submission))));
    }

    [AllowAnonymous]
    [HttpGet("/api/patito-ide/submissions/{submissionId:int}")]
    public async Task<IActionResult> GetIdeSubmissionStatusAsync(int submissionId)
    {
        return Ok(ToPatitoIdeSubmissionStatusResponse(await _ideSubmissionService.GetStatusAsync(Request.GetBearerToken(), submissionId)));
    }

    [AllowAnonymous]
    [HttpPost("/api/patito-ide/runs")]
    public async Task<IActionResult> RunFromIdeAsync([FromBody] PatitoIdeSubmissionForCreation submission)
    {
        return Ok(ToPatitoIdeRunResponse(await _ideSubmissionService.CustomInputAsync(Request.GetBearerToken(), ToIdeSubmissionRequest(submission))));
    }

    [AllowAnonymous]
    [HttpPost("/api/patito-ide/custom-input")]
    public async Task<IActionResult> CustomInputFromIdeAsync([FromBody] PatitoIdeSubmissionForCreation submission)
    {
        return Ok(ToPatitoIdeRunResponse(await _ideSubmissionService.CustomInputAsync(Request.GetBearerToken(), ToIdeSubmissionRequest(submission))));
    }

    [AllowAnonymous]
    [HttpGet("/api/patito-ide/runs/{runId:int}")]
    public async Task<IActionResult> GetIdeRunStatusAsync(int runId)
    {
        return Ok(ToPatitoIdeSubmissionStatusResponse(await _ideSubmissionService.GetStatusAsync(Request.GetBearerToken(), runId)));
    }

    private IdeSubmissionRequest ToIdeSubmissionRequest(PatitoIdeSubmissionForCreation submission)
    {
        return new IdeSubmissionRequest
        {
            SourceCode = submission.SourceCode,
            LanguageId = submission.LanguageId,
            ProblemId = submission.ProblemId,
            ContestId = submission.ContestId,
            Num = submission.Num,
            Stdin = submission.Stdin,
            ClientIp = ClientIpHelper.GetClientIp(HttpContext),
            Testcases = submission.Testcases
                .Select(item => new IdeTestcaseRequest
                {
                    Input = item.Input,
                    ExpectedOutput = item.ExpectedOutput
                })
                .ToArray()
        };
    }

    private static PatitoIdeSubmissionResponse ToPatitoIdeSubmissionResponse(IdeSubmissionResponse response)
    {
        return new PatitoIdeSubmissionResponse
        {
            SubmissionId = response.SubmissionId,
            Id = response.Id,
            StatusUrl = response.StatusUrl
        };
    }

    private static PatitoIdeRunResponse ToPatitoIdeRunResponse(IdeRunResponse response)
    {
        return new PatitoIdeRunResponse
        {
            RunId = response.RunId,
            Id = response.Id,
            StatusUrl = response.StatusUrl
        };
    }

    private static PatitoIdeSubmissionStatusResponse ToPatitoIdeSubmissionStatusResponse(IdeSubmissionStatusResponse response)
    {
        return new PatitoIdeSubmissionStatusResponse
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
