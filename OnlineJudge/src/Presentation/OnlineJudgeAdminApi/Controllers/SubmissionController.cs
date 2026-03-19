using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class SubmissionController : ControllerBase
{
    private readonly IAcademicService _academicService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public SubmissionController(IAcademicService academicService, UserClaimsHelper userClaimsHelper)
    {
        _academicService = academicService ?? throw new ArgumentNullException(nameof(academicService));
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
}
