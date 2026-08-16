using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/public")]
public class PublicController : ControllerBase
{
    private readonly IPublicService _publicService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public PublicController(IPublicService publicService, UserClaimsHelper userClaimsHelper)
    {
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [AllowAnonymous]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardAsync([FromQuery] int siteId = 1)
    {
        return Ok(await _publicService.GetDashboardAsync(siteId));
    }

    [AllowAnonymous]
    [HttpGet("problems")]
    public async Task<IActionResult> GetProblemsAsync(
        [FromQuery] int siteId = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? year = null,
        [FromQuery] string? contestTrack = null,
        [FromQuery] string? source = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int? contestId = null)
    {
        var currentUser = _userClaimsHelper.TryGetUserContextRole();
        return Ok(await _publicService.GetProblemsAsync(siteId, page, pageSize, searchTerm, year, contestTrack, source, tag, sortBy, contestId, currentUser));
    }

    [AllowAnonymous]
    [HttpGet("problems/filters")]
    public async Task<IActionResult> GetProblemFiltersAsync([FromQuery] int siteId = 1)
    {
        return Ok(await _publicService.GetProblemFiltersAsync(siteId));
    }

    [AllowAnonymous]
    [HttpGet("problems/{problemId:int}")]
    public async Task<IActionResult> GetProblemDetailAsync(int problemId, [FromQuery] int siteId = 1)
    {
        return Ok(await _publicService.GetProblemDetailAsync(siteId, problemId));
    }

    [AllowAnonymous]
    [HttpGet("problems/{problemId:int}/statistics")]
    public async Task<IActionResult> GetProblemStatisticsAsync(int problemId, [FromQuery] int siteId = 1)
    {
        return Ok(await _publicService.GetProblemStatisticsAsync(siteId, problemId));
    }

    [AllowAnonymous]
    [HttpGet("contests/{contestId:int}/problems/{contestProblemId}")]
    public async Task<IActionResult> GetContestProblemDetailAsync(int contestId, string contestProblemId, [FromQuery] int siteId = 1)
    {
        var currentUser = _userClaimsHelper.TryGetUserContextRole();
        return Ok(await _publicService.GetContestProblemDetailAsync(siteId, contestId, contestProblemId, currentUser));
    }

    [AllowAnonymous]
    [HttpGet("ranking")]
    public async Task<IActionResult> GetRankingAsync([FromQuery] int siteId = 1, [FromQuery] int limit = 50, [FromQuery] string? scope = null)
    {
        return Ok(await _publicService.GetRankingAsync(siteId, limit, scope));
    }

    [AllowAnonymous]
    [HttpGet("temas")]
    public async Task<IActionResult> GetTopicsAsync([FromQuery] int siteId = 1)
    {
        return Ok(await _publicService.GetTopicsAsync(siteId));
    }

    [AllowAnonymous]
    [HttpGet("contests")]
    public async Task<IActionResult> GetContestsAsync(
        [FromQuery] int siteId = 1,
        [FromQuery] string? status = null,
        [FromQuery] string? level = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? searchTerm = null)
    {
        return Ok(await _publicService.GetContestsAsync(siteId, status, level, sortBy, page, pageSize, searchTerm));
    }

    [AllowAnonymous]
    [HttpGet("contests/{contestId:int}/report")]
    public async Task<IActionResult> GetContestReportAsync(int contestId, [FromQuery] int siteId = 1)
    {
        var currentUser = _userClaimsHelper.TryGetUserContextRole();
        return Ok(await _publicService.GetContestReportAsync(siteId, contestId, currentUser));
    }

    [Authorize]
    [HttpPost("contests/{contestId:int}/register")]
    public async Task<IActionResult> RegisterForContestAsync(int contestId, [FromQuery] int siteId = 1)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var effectiveSiteId = currentUser.SiteId > 0 ? currentUser.SiteId : siteId;
        await _publicService.RegisterForContestAsync(currentUser, effectiveSiteId, contestId);
        return Ok();
    }

    [Authorize]
    [HttpGet("contests/{contestId:int}/report.csv")]
    public async Task<IActionResult> GetContestReportCsvAsync(int contestId, [FromQuery] int siteId = 1)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var effectiveSiteId = currentUser.SiteId > 0 ? currentUser.SiteId : siteId;
        var canDownload = await _publicService.CanDownloadContestReportCsvAsync(currentUser, effectiveSiteId, contestId);

        if (!canDownload)
        {
            return Forbid();
        }

        var report = await _publicService.GetContestReportAsync(effectiveSiteId, contestId, currentUser);
        var csv = BuildContestReportCsv(report);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"contest-{contestId}-report.csv");
    }

    [AllowAnonymous]
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguagesAsync()
    {
        return Ok(await _publicService.GetLanguagesAsync());
    }

    [AllowAnonymous]
    [HttpGet("submissions")]
    public async Task<IActionResult> GetSubmissionsAsync(
        [FromQuery] int siteId = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] int? contestId = null,
        [FromQuery] int? problemId = null,
        [FromQuery] string? userId = null,
        [FromQuery] int? languageId = null,
        [FromQuery] string? statusKey = null)
    {
        var currentUser = _userClaimsHelper.TryGetUserContextRole();
        return Ok(await _publicService.GetSubmissionsAsync(siteId, page, pageSize, contestId, problemId, userId, languageId, statusKey, currentUser));
    }

    [Authorize]
    [HttpGet("submissions/mine")]
    public async Task<IActionResult> GetOwnSubmissionsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _publicService.GetOwnSubmissionsAsync(currentUser, page, pageSize));
    }

    [Authorize]
    [HttpGet("activity/online-users")]
    public async Task<IActionResult> GetOnlineUsersAsync(
        [FromQuery] int siteId = 1,
        [FromQuery] int windowMinutes = 10)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var effectiveSiteId = currentUser.SiteId > 0 ? currentUser.SiteId : siteId;
        return Ok(await _publicService.GetOnlineUsersAsync(effectiveSiteId, windowMinutes));
    }

    [AllowAnonymous]
    [HttpGet("activity/recent-submissions")]
    public async Task<IActionResult> GetRecentSubmissionsAsync(
        [FromQuery] int siteId = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        return Ok(await _publicService.GetRecentSubmissionsAsync(siteId, page, pageSize, null, null));
    }

    [AllowAnonymous]
    [HttpGet("activity/contest/{contestId:int}/recent-submissions")]
    public async Task<IActionResult> GetContestRecentSubmissionsAsync(
        int contestId,
        [FromQuery] int siteId = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var currentUser = _userClaimsHelper.TryGetUserContextRole();
        return Ok(await _publicService.GetRecentSubmissionsAsync(siteId, page, pageSize, contestId, null, currentUser));
    }

    [Authorize]
    [HttpGet("activity/course/{courseId:long}/recent-submissions")]
    public async Task<IActionResult> GetCourseRecentSubmissionsAsync(
        long courseId,
        [FromQuery] int siteId = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var effectiveSiteId = currentUser.SiteId > 0 ? currentUser.SiteId : siteId;
        return Ok(await _publicService.GetRecentSubmissionsAsync(effectiveSiteId, page, pageSize, null, courseId));
    }

    [Authorize]
    [HttpGet("submissions/mine/source-codes.zip")]
    public async Task<IActionResult> DownloadOwnSourceCodesAsync()
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var items = await _publicService.GetOwnSubmissionSourceCodesAsync(currentUser);

        if (items.Count == 0)
        {
            return NotFound(new ErrorDetails { StatusCode = 404, Message = "No hay códigos fuente disponibles para descargar." });
        }

        var archiveBytes = BuildOwnSourceCodesArchive(items);
        var fileName = $"source-codes-{currentUser.UserId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        return File(archiveBytes, "application/zip", fileName);
    }

    [Authorize]
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAsync(PublicSubmissionForCreation submissionForCreation)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        var request = new PublicSubmissionRequest
        {
            ProblemId = submissionForCreation.ProblemId,
            ContestProblemId = submissionForCreation.ContestProblemId,
            SourceCode = submissionForCreation.SourceCode,
            LanguageId = submissionForCreation.LanguageId,
            ContestId = submissionForCreation.ContestId,
            Num = submissionForCreation.Num,
            CourseId = submissionForCreation.CourseId,
            AssignmentId = submissionForCreation.AssignmentId,
            FileName = submissionForCreation.FileName,
            ClientIp = ClientIpHelper.GetClientIp(HttpContext)
        };

        return Ok(await _publicService.SubmitAsync(currentUser, request));
    }

    [Authorize]
    [HttpGet("submissions/{solutionId:int}")]
    public async Task<IActionResult> GetSubmissionStatusAsync(int solutionId)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _publicService.GetSubmissionStatusAsync(currentUser, solutionId));
    }

    private static string BuildContestReportCsv(ContestReportResponse report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("rank,userId,nick,school,solved,submissions,accepted,accuracy,firstSubmitUtc,lastSubmitUtc");

        foreach (var item in report.Items)
        {
            builder.Append(item.Rank).Append(',');
            builder.Append(EscapeCsv(item.UserId)).Append(',');
            builder.Append(EscapeCsv(item.Nick)).Append(',');
            builder.Append(EscapeCsv(item.School)).Append(',');
            builder.Append(item.Solved).Append(',');
            builder.Append(item.Submissions).Append(',');
            builder.Append(item.Accepted).Append(',');
            builder.Append(item.Accuracy.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            builder.Append(item.FirstSubmitUtc?.ToString("O") ?? string.Empty).Append(',');
            builder.Append(item.LastSubmitUtc?.ToString("O") ?? string.Empty).AppendLine();
        }

        return builder.ToString();
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

    private static byte[] BuildOwnSourceCodesArchive(IReadOnlyCollection<PublicSubmissionSourceCodeItem> items)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.csv");
            using (var manifestStream = manifestEntry.Open())
            using (var writer = new StreamWriter(manifestStream, new UTF8Encoding(false)))
            {
                writer.WriteLine("solutionId,problemId,problemTitle,languageId,languageName,statusKey,createdAtUtc,fileName");

                foreach (var item in items)
                {
                    var relativePath = BuildSourceCodeRelativePath(item);
                    writer.Write(item.SolutionId);
                    writer.Write(',');
                    writer.Write(item.ProblemId);
                    writer.Write(',');
                    writer.Write(EscapeCsv(item.ProblemTitle));
                    writer.Write(',');
                    writer.Write(item.LanguageId);
                    writer.Write(',');
                    writer.Write(EscapeCsv(item.LanguageName));
                    writer.Write(',');
                    writer.Write(EscapeCsv(item.StatusKey));
                    writer.Write(',');
                    writer.Write(item.CreatedAtUtc.ToString("O"));
                    writer.Write(',');
                    writer.WriteLine(EscapeCsv(relativePath));
                }
            }

            foreach (var item in items)
            {
                var entry = archive.CreateEntry(BuildSourceCodeRelativePath(item));
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                writer.Write(item.SourceCode);
            }
        }

        return stream.ToArray();
    }

    private static string BuildSourceCodeRelativePath(PublicSubmissionSourceCodeItem item)
    {
        var createdAt = item.CreatedAtUtc.ToString("yyyyMMdd-HHmmss");
        var fileBaseName = $"{item.SolutionId:D8}-{createdAt}-{Slugify(item.ProblemTitle)}-{item.StatusKey}";
        return $"codes/{fileBaseName}{InferExtension(item.LanguageName)}";
    }

    private static string InferExtension(string languageName)
    {
        var normalized = languageName.Trim().ToLowerInvariant();

        if (normalized.Contains("c++") || normalized.Contains("cpp"))
        {
            return ".cpp";
        }

        if (normalized == "c" || normalized.StartsWith("c "))
        {
            return ".c";
        }

        if (normalized.Contains("java"))
        {
            return ".java";
        }

        if (normalized.Contains("python"))
        {
            return ".py";
        }

        if (normalized.Contains("javascript"))
        {
            return ".js";
        }

        if (normalized.Contains("typescript"))
        {
            return ".ts";
        }

        if (normalized.Contains("c#") || normalized.Contains("csharp"))
        {
            return ".cs";
        }

        if (normalized.Contains("go"))
        {
            return ".go";
        }

        if (normalized.Contains("rust"))
        {
            return ".rs";
        }

        if (normalized.Contains("pascal"))
        {
            return ".pas";
        }

        if (normalized.Contains("kotlin"))
        {
            return ".kt";
        }

        return ".txt";
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "problem";
        }

        var builder = new StringBuilder(value.Length);

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length == 0 || builder[^1] == '-')
            {
                continue;
            }

            builder.Append('-');
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "problem" : normalized;
    }
}
