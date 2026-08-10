using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/academic")]
[Authorize]
public class AcademicController : ControllerBase
{
    private readonly IAcademicService _academicService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public AcademicController(IAcademicService academicService, UserClaimsHelper userClaimsHelper)
    {
        _academicService = academicService ?? throw new ArgumentNullException(nameof(academicService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpGet("sites/{siteId:int}/institutions")]
    public async Task<IActionResult> GetInstitutionsAsync(int siteId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetInstitutionsAsync(siteId, currentUser));
    }

    [HttpGet("sites/{siteId:int}/institutions/ranking")]
    public async Task<IActionResult> GetInstitutionRankingAsync(int siteId, [FromQuery] int limit = 20)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetInstitutionsRankingAsync(siteId, currentUser, limit));
    }

    [HttpGet("sites/{siteId:int}/courses/mine")]
    public async Task<IActionResult> GetMyCoursesAsync(int siteId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetMyCoursesAsync(siteId, currentUser));
    }

    [HttpGet("sites/{siteId:int}/courses/manageable")]
    public async Task<IActionResult> GetManageableCoursesAsync(int siteId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetManageableCoursesAsync(siteId, currentUser));
    }

    [HttpPost("sites/{siteId:int}/courses")]
    [Authorize(Roles = "Administrador,Docente,Auxiliar")]
    public async Task<IActionResult> CreateCourseAsync(int siteId, AcademicCourseForCreation courseForCreation)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();

        var request = new AcademicCourseCreationRequest
        {
            Name = courseForCreation.Name,
            Description = courseForCreation.Description,
            InstitutionId = courseForCreation.InstitutionId
        };

        return Ok(await _academicService.CreateCourseAsync(siteId, currentUser, request));
    }

    [HttpPost("sites/{siteId:int}/courses/join")]
    public async Task<IActionResult> JoinCourseAsync(int siteId, AcademicJoinCourseForCreation joinCourse)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();

        var request = new AcademicJoinCourseRequest
        {
            InviteCode = joinCourse.InviteCode
        };

        return Ok(await _academicService.JoinCourseAsync(siteId, currentUser, request));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}")]
    public async Task<IActionResult> GetCourseAsync(int siteId, long courseId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetCourseAsync(siteId, courseId, currentUser));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/members")]
    public async Task<IActionResult> GetCourseMembersAsync(int siteId, long courseId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetCourseMembersAsync(siteId, courseId, currentUser));
    }

    [HttpPost("sites/{siteId:int}/courses/{courseId:long}/members")]
    public async Task<IActionResult> AddCourseMemberAsync(int siteId, long courseId, AcademicCourseMemberForCreation memberForCreation)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();

        var request = new AcademicCourseMemberCreationRequest
        {
            UserId = memberForCreation.UserId,
            Role = memberForCreation.Role
        };

        return Ok(await _academicService.AddCourseMemberAsync(siteId, courseId, currentUser, request));
    }

    [HttpDelete("sites/{siteId:int}/courses/{courseId:long}/members/{memberUserId}")]
    public async Task<IActionResult> RemoveCourseMemberAsync(int siteId, long courseId, string memberUserId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        await _academicService.RemoveCourseMemberAsync(siteId, courseId, memberUserId, currentUser);
        return NoContent();
    }

    [HttpPost("sites/{siteId:int}/courses/{courseId:long}/assignments")]
    public async Task<IActionResult> CreateCourseAssignmentAsync(int siteId, long courseId, AcademicAssignmentForCreation assignmentForCreation)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();

        var request = new AcademicCourseAssignmentCreationRequest
        {
            Title = assignmentForCreation.Title,
            Description = assignmentForCreation.Description,
            OpensAt = assignmentForCreation.OpensAt,
            DueAt = assignmentForCreation.DueAt,
            LateDueAt = assignmentForCreation.LateDueAt,
            IsActive = assignmentForCreation.IsActive,
            ProblemIds = assignmentForCreation.ProblemIds
        };

        return Ok(await _academicService.CreateCourseAssignmentAsync(siteId, courseId, currentUser, request));
    }

    [HttpPost("sites/{siteId:int}/courses/{courseId:long}/materials")]
    public async Task<IActionResult> CreateCourseMaterialAsync(int siteId, long courseId, AcademicCourseMaterialForCreation material)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.CreateCourseMaterialAsync(siteId, courseId, currentUser, new AcademicCourseMaterialCreationRequest
        {
            Title = material.Title,
            Description = material.Description,
            ContentUrl = material.ContentUrl,
            ContentBody = material.ContentBody,
            IsPublished = material.IsPublished
        }));
    }

    [HttpPut("sites/{siteId:int}/courses/{courseId:long}/content-order")]
    public async Task<IActionResult> ReorderCourseContentAsync(int siteId, long courseId, AcademicCourseContentOrderForUpdate orderForUpdate)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        await _academicService.ReorderCourseContentAsync(siteId, courseId, currentUser, orderForUpdate.ItemIds ?? new List<long>());
        return NoContent();
    }

    [HttpPut("sites/{siteId:int}/courses/{courseId:long}/assignments/{assignmentId:long}")]
    public async Task<IActionResult> UpdateCourseAssignmentAsync(int siteId, long courseId, long assignmentId, AcademicAssignmentForCreation assignmentForCreation)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();

        var request = new AcademicCourseAssignmentCreationRequest
        {
            Title = assignmentForCreation.Title,
            Description = assignmentForCreation.Description,
            OpensAt = assignmentForCreation.OpensAt,
            DueAt = assignmentForCreation.DueAt,
            LateDueAt = assignmentForCreation.LateDueAt,
            IsActive = assignmentForCreation.IsActive,
            ProblemIds = assignmentForCreation.ProblemIds
        };

        return Ok(await _academicService.UpdateCourseAssignmentAsync(siteId, courseId, assignmentId, currentUser, request));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/assignments/{assignmentId:long}")]
    public async Task<IActionResult> GetCourseAssignmentAsync(int siteId, long courseId, long assignmentId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetCourseAssignmentAsync(siteId, courseId, assignmentId, currentUser));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/assignments/{assignmentId:long}/submissions")]
    public async Task<IActionResult> GetCourseAssignmentSubmissionsAsync(
        int siteId,
        long courseId,
        long assignmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetCourseAssignmentSubmissionsAsync(siteId, courseId, assignmentId, page, pageSize, currentUser));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/ranking")]
    public async Task<IActionResult> GetCourseRankingAsync(int siteId, long courseId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetCourseRankingAsync(siteId, courseId, currentUser));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/report")]
    public async Task<IActionResult> GetCourseReportAsync(int siteId, long courseId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetCourseReportAsync(siteId, courseId, currentUser));
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/report.csv")]
    public async Task<IActionResult> DownloadCourseReportCsvAsync(int siteId, long courseId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var report = await _academicService.GetCourseReportAsync(siteId, courseId, currentUser);
        if (!report.CanDownloadCsv)
        {
            return Forbid();
        }

        var csvContent = BuildCourseReportCsv(report);
        var fileName = $"course-{courseId}-report.csv";
        return File(Encoding.UTF8.GetBytes(csvContent), "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("sites/{siteId:int}/courses/{courseId:long}/students/{userId}/progress")]
    public async Task<IActionResult> GetStudentProgressAsync(int siteId, long courseId, string userId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetStudentProgressAsync(siteId, courseId, userId, currentUser));
    }

    [AllowAnonymous]
    [HttpGet("sites/{siteId:int}/learning-paths")]
    public async Task<IActionResult> GetLearningPathsAsync(int siteId)
    {
        var currentUser = User.Identity?.IsAuthenticated == true
            ? _userClaimsHelper.GetUserContextRole()
            : new CurrentUser
            {
                SiteId = siteId,
                UserId = "public",
                Role = UserRolesEnum.Invitado
            };

        return Ok(await _academicService.GetLearningPathsAsync(siteId, currentUser));
    }

    [AllowAnonymous]
    [HttpGet("sites/{siteId:int}/learning-paths/{learningPathKey}")]
    public async Task<IActionResult> GetLearningPathAsync(int siteId, string learningPathKey)
    {
        var currentUser = User.Identity?.IsAuthenticated == true
            ? _userClaimsHelper.GetUserContextRole()
            : new CurrentUser
            {
                SiteId = siteId,
                UserId = "public",
                Role = UserRolesEnum.Invitado
            };

        return Ok(await _academicService.GetLearningPathAsync(siteId, learningPathKey, currentUser));
    }

    [HttpGet("sites/{siteId:int}/learning-paths/{learningPathKey}/progress")]
    public async Task<IActionResult> GetLearningPathProgressAsync(int siteId, string learningPathKey)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _academicService.GetLearningPathProgressAsync(siteId, learningPathKey, currentUser));
    }

    [HttpPut("sites/{siteId:int}/learning-paths/{learningPathKey}/progress")]
    public async Task<IActionResult> SaveLearningPathProgressAsync(
        int siteId,
        string learningPathKey,
        AcademicLearningPathProgressForUpdate progressForUpdate)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        progressForUpdate ??= new AcademicLearningPathProgressForUpdate();
        var request = new LearningPathProgressUpdateRequest
        {
            LastTopicId = progressForUpdate.LastTopicId,
            CompletedTopicIds = progressForUpdate.CompletedTopicIds
        };

        return Ok(await _academicService.SaveLearningPathProgressAsync(siteId, learningPathKey, currentUser, request));
    }

    private static string BuildCourseReportCsv(AcademicCourseReportResponse report)
    {
        var builder = new StringBuilder();

        // Title row: the contest/material title spans its own 3 columns (solved, attempts, accepted),
        // matching the grouped header shown on screen.
        var titleRow = new List<string> { "", "", "", "", "", "" };
        var headerRow = new List<string> { "rank", "userId", "nick", "totalSolved", "totalAttempts", "totalAccepted" };
        foreach (var assignment in report.Assignments)
        {
            var normalizedTitle = BuildCsvHeaderKey(assignment.Title);
            titleRow.Add(assignment.Title);
            titleRow.Add(string.Empty);
            titleRow.Add(string.Empty);
            headerRow.Add(normalizedTitle + "_solved");
            headerRow.Add(normalizedTitle + "_attempts");
            headerRow.Add(normalizedTitle + "_accepted");
        }

        builder.AppendLine(string.Join(',', titleRow.Select(EscapeCsv)));
        builder.AppendLine(string.Join(',', headerRow.Select(EscapeCsv)));

        foreach (var item in report.Items)
        {
            var row = new List<string>
            {
                item.Rank.ToString(),
                item.UserId,
                item.Nick,
                item.TotalSolved.ToString(),
                item.TotalAttempts.ToString(),
                item.TotalAccepted.ToString()
            };

            foreach (var assignment in report.Assignments)
            {
                var stats = item.Assignments.FirstOrDefault(cell => cell.AssignmentId == assignment.AssignmentId);
                row.Add((stats?.Solved ?? 0).ToString());
                row.Add((stats?.Attempts ?? 0).ToString());
                row.Add((stats?.Accepted ?? 0).ToString());
            }

            builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildCsvHeaderKey(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "assignment" : value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length == 0 || builder[^1] == '_')
            {
                continue;
            }

            builder.Append('_');
        }

        var result = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "assignment" : result;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{escaped}\""
            : escaped;
    }
}
