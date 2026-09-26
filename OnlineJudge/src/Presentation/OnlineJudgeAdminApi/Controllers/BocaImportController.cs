using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.BocaImporter;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Two-step flow: /preview extracts each uploaded zip and parses it (same
// BocaPackageReader the CLI importer uses) without touching the database, so the admin
// can see title/limits/needs-review before committing. /confirm re-reads the staged
// folder for the ids the admin picked and actually creates the problems.
[ApiController]
[Route("/api/boca-import")]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public sealed class BocaImportController : ControllerBase
{
    private static readonly string StagingRoot = Path.Combine(Path.GetTempPath(), "boca-import");
    private const string ArchiveRoot = "/boca-archive";

    private readonly IProblemService _problemService;
    private readonly IProblemClassifierService _problemClassifierService;
    private readonly IFileSystemLocalManagerManager _fileManager;
    private readonly UserClaimsHelper _userClaimsHelper;

    public BocaImportController(
        IProblemService problemService,
        IProblemClassifierService problemClassifierService,
        IFileSystemLocalManagerManager fileManager,
        UserClaimsHelper userClaimsHelper)
    {
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _problemClassifierService = problemClassifierService ?? throw new ArgumentNullException(nameof(problemClassifierService));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
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
            var stagingId = Guid.NewGuid().ToString("N");
            var stagingDir = Path.Combine(StagingRoot, stagingId);

            try
            {
                Directory.CreateDirectory(stagingDir);
                // Kept (not deleted) until /confirm: on success it's copied to ArchiveRoot
                // under its own name, so the original package survives even though
                // extractDir gets thrown away.
                var zipPath = Path.Combine(stagingDir, Path.GetFileName(file.FileName));
                using (var stream = System.IO.File.Create(zipPath))
                {
                    file.CopyTo(stream);
                }

                var extractDir = Path.Combine(stagingDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                var (problem, needsReview, reasons, suggestion) = await BuildProblemAsync(extractDir, file.FileName);
                var testCaseCount = BocaPackageReader.ReadAllTestCases(extractDir).Count;

                results.Add(new BocaImportPreviewResult
                {
                    StagingId = stagingId,
                    FileName = file.FileName,
                    Success = true,
                    Title = problem.Title,
                    TimeLimit = problem.TimeLimit,
                    MemoryLimit = problem.MemoryLimit,
                    NeedsReview = needsReview,
                    ReviewReasons = reasons,
                    TestCaseCount = testCaseCount,
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
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }

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
            // stagingId comes straight from the request body and is used to build a filesystem
            // path that later gets recursively deleted - only ever accept the "N"-format GUID
            // /preview hands out, never a path fragment (e.g. "../../etc") from an untrusted caller.
            if (!Guid.TryParseExact(stagingId, "N", out _))
            {
                results.Add(new BocaImportConfirmResult { StagingId = stagingId, Success = false, Error = "Invalid staging id." });
                continue;
            }

            var stagingDir = Path.Combine(StagingRoot, stagingId);
            var extractDir = Path.Combine(stagingDir, "extracted");

            try
            {
                if (!Directory.Exists(extractDir))
                {
                    throw new InvalidOperationException("No se encontró el paquete preparado (puede haber expirado) - vuelve a subirlo.");
                }

                var (problem, _, _, _) = await BuildProblemAsync(extractDir, stagingId);
                var selectedIds = request.SelectedClassificationIdsByStagingId?.GetValueOrDefault(stagingId);
                if (selectedIds is { Count: > 0 })
                {
                    problem.Classifications = selectedIds.Select(id => new Classification { ClassificationId = id }).ToList();
                }

                var testCases = BocaPackageReader.ReadAllTestCases(extractDir);
                var created = await _problemService.CreateProblemAsync(userId, problem, currentUser.SiteId);

                var problemId = created.ProblemId!.Value.ToString();
                for (var i = 0; i < testCases.Count; i++)
                {
                    var caseNumber = i + 1;
                    _fileManager.WriteToFile(problemId, $"{caseNumber}.in", testCases[i].Input);
                    _fileManager.WriteToFile(problemId, $"{caseNumber}.out", testCases[i].Output);
                }

                ArchiveOriginalPackage(stagingDir, problemId);

                results.Add(new BocaImportConfirmResult { StagingId = stagingId, Success = true, ProblemId = created.ProblemId });
            }
            catch (Exception error)
            {
                results.Add(new BocaImportConfirmResult { StagingId = stagingId, Success = false, Error = error.Message });
            }
            finally
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
            }
        }

        return Ok(new { results });
    }

    // Keeps a permanent copy of the uploaded .zip (compile/run/compare scripts, every raw
    // test case, any compiled checker binary) - none of that survives into the DB or
    // /judge-data, and boca_problems/ itself isn't tracked by any git repo.
    private static void ArchiveOriginalPackage(string stagingDir, string problemId)
    {
        // TopDirectoryOnly + GetFiles already excludes the extracted/ subdirectory; the
        // staged zip is the only file that sits directly in stagingDir.
        var originalZip = Directory.GetFiles(stagingDir, "*", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (originalZip is null)
        {
            return;
        }

        Directory.CreateDirectory(ArchiveRoot);
        var archivePath = Path.Combine(ArchiveRoot, $"{problemId}-{Path.GetFileName(originalZip)}");
        System.IO.File.Copy(originalZip, archivePath, overwrite: true);
    }

    private async Task<(Problem Problem, bool NeedsReview, List<string> ReviewReasons, ProblemClassificationSuggestion Suggestion)> BuildProblemAsync(
        string extractDir, string sourceName)
    {
        var reviewReasons = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(sourceName);

        var info = BocaPackageReader.ReadProblemInfo(extractDir, baseName);
        var description = await BocaPackageReader.ReadDescriptionAsync(extractDir, info.DescFile);
        var sample = BocaPackageReader.ReadSamplePair(extractDir);
        var limits = BocaPackageReader.CollectLimits(extractDir);

        if (description.NeedsReview) reviewReasons.Add($"description: {description.ReviewReason}");
        if (sample.NeedsReview) reviewReasons.Add($"sample: {sample.ReviewReason}");
        var needsReview = reviewReasons.Count > 0;

        var problem = new Problem
        {
            Title = info.FullName,
            Description = description.Html,
            Input = description.InputHtml,
            Output = description.OutputHtml,
            SampleInput = sample.Input,
            SampleOutput = sample.Output,
            Hint = description.HintHtml,
            Source = $"BOCA import ({info.BaseName})",
            OriginSource = "BOCA import",
            TimeLimit = limits.TimeLimitSeconds,
            MemoryLimit = limits.MemoryLimitMb,
            Spj = "N",
            Defunct = "N",
            InDate = DateTime.Now,
        };

        var suggestion = await _problemClassifierService.SuggestClassificationsAsync(problem);
        return (problem, needsReview, reviewReasons, suggestion);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
