using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Two-step flow: /preview reads each uploaded zip without touching the database, so the admin
// can see title/limits/needs-review before committing. /confirm creates the problems
// for the ids the admin picked. The work is in BocaImportService.
[ApiController]
[Route("/api/boca-import")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public sealed class BocaImportController : ControllerBase
{
    private readonly IBocaImportService _importService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public BocaImportController(IBocaImportService importService, UserClaimsHelper userClaimsHelper)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpPost("preview")]
    [RequestSizeLimit(200_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
    public async Task<IActionResult> Preview(List<IFormFile> files)
    {
        var results = new List<BocaImportPreviewResult>();

        foreach (var file in files)
        {
            try
            {
                await using var content = file.OpenReadStream();
                var preview = await _importService.PreviewAsync(content, file.FileName);
                var (problem, suggestion) = (preview.Problem, preview.Suggestion);

                results.Add(new BocaImportPreviewResult
                {
                    StagingId = preview.StagingId,
                    FileName = file.FileName,
                    Success = true,
                    Title = problem.Title,
                    TimeLimit = problem.TimeLimit,
                    MemoryLimit = problem.MemoryLimit,
                    NeedsReview = preview.ReviewReasons.Count > 0,
                    ReviewReasons = preview.ReviewReasons.ToList(),
                    TestCaseCount = preview.TestCaseCount,
                    DescriptionPreview = Truncate(problem.Description, 300),
                    SampleInputPreview = Truncate(problem.SampleInput, 300),
                    SampleOutputPreview = Truncate(problem.SampleOutput, 300),
                    SuggestedClassifications = suggestion.Classifications
                        .Select(c => new BocaClassificationSuggestionDto
                        {
                            ClassificationId = c.ClassificationId,
                            Label = c.Topic is not null ? $"{c.Topic.Name} > {c.Name}" : c.Name,
                            Reason = suggestion.Reasons.TryGetValue(c.ClassificationId, out var reason) ? reason : string.Empty,
                        })
                        .ToList(),
                });
            }
            catch (Exception error)
            {
                results.Add(new BocaImportPreviewResult
                {
                    FileName = file.FileName,
                    Success = false,
                    Error = error.Message,
                });
            }
        }

        return Ok(new { results });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] BocaImportConfirmRequest request)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var userId = (User.Identity as ClaimsIdentity)?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? currentUser.UserId;

        var results = new List<BocaImportConfirmResult>();

        foreach (var stagingId in request.StagingIds)
        {
            try
            {
                var selectedIds = request.SelectedClassificationIdsByStagingId?.GetValueOrDefault(stagingId);
                var problemId = await _importService.ConfirmAsync(stagingId, userId, currentUser.SiteId, selectedIds);
                results.Add(new BocaImportConfirmResult { StagingId = stagingId, Success = true, ProblemId = problemId });
            }
            catch (Exception error)
            {
                results.Add(new BocaImportConfirmResult { StagingId = stagingId, Success = false, Error = error.Message });
            }
        }

        return Ok(new { results });
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
