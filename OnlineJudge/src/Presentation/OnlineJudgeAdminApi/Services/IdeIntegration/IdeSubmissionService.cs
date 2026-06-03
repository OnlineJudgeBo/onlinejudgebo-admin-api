using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed class IdeSubmissionService : IIdeSubmissionService
{
    private readonly IIdeLaunchTokenValidator _tokenValidator;
    private readonly IPublicService _publicService;

    public IdeSubmissionService(
        IIdeLaunchTokenValidator tokenValidator,
        IPublicService publicService)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
    }

    public async Task<VibeSubmissionResponse> SubmitAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        var response = await SubmitToJudgeAsync(launchToken, submission);
        var id = response.SolutionId.ToString();

        return new VibeSubmissionResponse
        {
            SubmissionId = id,
            Id = id,
            StatusUrl = $"/api/vibe/submissions/{id}"
        };
    }

    public async Task<VibeRunResponse> RunAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        var response = await SubmitToJudgeAsync(launchToken, submission);
        var id = response.SolutionId.ToString();

        return new VibeRunResponse
        {
            RunId = id,
            Id = id,
            StatusUrl = $"/api/vibe/runs/{id}"
        };
    }

    public async Task<VibeSubmissionStatusResponse> GetStatusAsync(string launchToken, int solutionId)
    {
        var claims = ValidateLaunchToken(launchToken);
        var status = await _publicService.GetSubmissionStatusAsync(ToCurrentUser(claims), solutionId);
        var id = status.SolutionId.ToString();

        return new VibeSubmissionStatusResponse
        {
            SubmissionId = id,
            Id = id,
            Phase = ToPhase(status),
            Verdict = ToVerdict(status),
            Stdout = string.Empty,
            Stderr = status.RuntimeMessage ?? string.Empty,
            CompileErrors = status.CompileMessage ?? string.Empty,
            Logs = new[]
            {
                status.StatusLabel,
                $"Tiempo: {status.TimeMs} ms",
                $"Memoria: {status.MemoryKb} KB"
            },
            RuntimeMs = status.TimeMs,
            MemoryKb = status.MemoryKb
        };
    }

    private async Task<PublicSubmissionResponse> SubmitToJudgeAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        var claims = ValidateLaunchToken(launchToken);
        var contestId = ResolveContestId(claims, submission);

        var contestProblemId = ResolveContestProblemId(claims, submission, contestId);

        return await _publicService.SubmitAsync(ToCurrentUser(claims), new PublicSubmissionRequest
        {
            ProblemId = ResolveProblemId(claims, submission),
            ContestId = contestId,
            ContestProblemId = contestProblemId,
            SourceCode = submission.SourceCode,
            LanguageId = ResolveLanguageId(claims, submission)
        });
    }

    private static int? ResolveContestId(IdeLaunchClaims claims, VibeSubmissionForCreation submission)
    {
        if (claims.ContestId.HasValue)
        {
            if (submission.ContestId.HasValue && submission.ContestId.Value > 0 && submission.ContestId.Value != claims.ContestId.Value)
            {
                throw new UnauthorizedAccessException("El token de IDE no permite enviar a este concurso.");
            }

            return claims.ContestId;
        }

        return submission.ContestId.HasValue && submission.ContestId.Value > 0
            ? submission.ContestId.Value
            : null;
    }

    private static string? ResolveContestProblemId(IdeLaunchClaims claims, VibeSubmissionForCreation submission, int? contestId)
    {
        if (!contestId.HasValue || contestId.Value <= 0)
        {
            return null;
        }

        if (claims.Num.HasValue && claims.Num.Value >= 0)
        {
            if (submission.Num.HasValue && submission.Num.Value >= 0 && submission.Num.Value != claims.Num.Value)
            {
                throw new UnauthorizedAccessException("El token de IDE no permite enviar a este problema del concurso.");
            }

            return ContestProblemCode.FromNumber(claims.Num.Value);
        }

        return submission.Num.HasValue && submission.Num.Value >= 0
            ? ContestProblemCode.FromNumber(submission.Num.Value)
            : null;
    }

    private IdeLaunchClaims ValidateLaunchToken(string launchToken)
    {
        try
        {
            return _tokenValidator.Validate(launchToken);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new UnauthorizedAccessException("Token de IDE inválido o expirado.", error);
        }
    }

    private static CurrentUser ToCurrentUser(IdeLaunchClaims claims)
    {
        return new CurrentUser
        {
            UserId = claims.UserId,
            SiteId = claims.SiteId,
            Role = UserRolesEnum.Invitado
        };
    }

    private static int ResolveProblemId(IdeLaunchClaims claims, VibeSubmissionForCreation submission)
    {
        var requestProblemId = submission.ProblemIdAsInt();
        var problemId = requestProblemId > 0 ? requestProblemId : claims.ProblemId;

        if (problemId <= 0)
        {
            throw new ArgumentException("ProblemId es requerido.");
        }

        if (claims.ProblemId > 0 && problemId != claims.ProblemId)
        {
            throw new UnauthorizedAccessException("El token de IDE no permite enviar a este problema.");
        }

        return problemId;
    }

    private static int ResolveLanguageId(IdeLaunchClaims claims, VibeSubmissionForCreation submission)
    {
        if (!submission.LanguageId.HasValue || submission.LanguageId.Value <= 0)
        {
            throw new ArgumentException("LanguageId es requerido.");
        }

        var languageId = submission.LanguageId.Value;
        if (claims.AllowedLanguages.Length > 0 && !claims.AllowedLanguages.Contains(languageId))
        {
            throw new UnauthorizedAccessException("El token de IDE no permite usar este lenguaje.");
        }

        return languageId;
    }

    private static string ToPhase(PublicSubmissionStatusResponse status)
    {
        if (status.IsFinal)
        {
            return "completed";
        }

        return status.GeneralStatusKey switch
        {
            "queued" => "queued",
            "evaluating" => "running",
            _ => "running"
        };
    }

    private static string ToVerdict(PublicSubmissionStatusResponse status)
    {
        return status.StatusKey switch
        {
            "accepted" => "Accepted",
            "wrong_answer" or "presentation_error" => "Wrong Answer",
            "compile_error" => "Compilation Error",
            "runtime_error" => "Runtime Error",
            "time_limit_exceeded" => "Time Limit Exceeded",
            "memory_limit_exceeded" or "output_limit_exceeded" => "Internal Error",
            _ => "Pending"
        };
    }
}
