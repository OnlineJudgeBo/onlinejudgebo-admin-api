using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations.IdeIntegration;

public sealed class IdeSubmissionService : IIdeSubmissionService
{
    private readonly IIdeLaunchTokenValidator _tokenValidator;
    private readonly IPublicService _publicService;
    private readonly IIdeCustomInputRepository _customInputRepository;

    public IdeSubmissionService(
        IIdeLaunchTokenValidator tokenValidator,
        IPublicService publicService,
        IIdeCustomInputRepository customInputRepository)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
        _customInputRepository = customInputRepository ?? throw new ArgumentNullException(nameof(customInputRepository));
    }

    public async Task<IdeSubmissionResponse> SubmitAsync(string launchToken, IdeSubmissionRequest submission)
    {
        PublicSubmissionResponse response = await SubmitToJudgeAsync(launchToken, submission);
        string id = response.SolutionId.ToString();

        return new IdeSubmissionResponse
        {
            SubmissionId = id,
            Id = id,
            StatusUrl = $"/api/patito-ide/submissions/{id}"
        };
    }

    public Task<IdeRunResponse> RunAsync(string launchToken, IdeSubmissionRequest submission)
    {
        return CustomInputAsync(launchToken, submission);
    }

    public async Task<IdeRunResponse> CustomInputAsync(string launchToken, IdeSubmissionRequest submission)
    {
        IdeLaunchClaims claims = ValidateLaunchToken(launchToken);
        ValidateSourceCode(submission.SourceCode);
        int? contestId = ResolveContestId(claims, submission);
        _ = ResolveContestProblemId(claims, submission, contestId);
        int problemId = ResolveProblemId(claims, submission);
        int languageId = ResolveLanguageId(claims, submission);

        int solutionId = await _customInputRepository.CreateCustomInputRunAsync(new IdeCustomInputRunCreation(
            claims.UserId,
            claims.SiteId,
            problemId,
            languageId,
            contestId,
            claims.Num ?? submission.Num ?? -1,
            submission.SourceCode,
            submission.Testcases,
            submission.Stdin,
            submission.ClientIp));

        string id = solutionId.ToString();
        return new IdeRunResponse
        {
            RunId = id,
            Id = id,
            StatusUrl = $"/api/patito-ide/runs/{id}"
        };
    }

    public async Task<IdeSubmissionStatusResponse> GetStatusAsync(string launchToken, int solutionId)
    {
        IdeLaunchClaims claims = ValidateLaunchToken(launchToken);
        PublicSubmissionStatusResponse status = await _publicService.GetSubmissionStatusAsync(ToCurrentUser(claims), solutionId);
        bool isCustomInput = await _customInputRepository.IsCustomInputAsync(solutionId);
        string id = status.SolutionId.ToString();

        return new IdeSubmissionStatusResponse
        {
            SubmissionId = id,
            Id = id,
            Phase = ToPhase(status, isCustomInput),
            Verdict = ToVerdict(status, isCustomInput),
            Stdout = isCustomInput && status.ResultCode == JudgeResultCodes.TestRunDone ? status.RuntimeMessage ?? string.Empty : string.Empty,
            Stderr = isCustomInput && status.ResultCode == JudgeResultCodes.TestRunDone ? string.Empty : status.RuntimeMessage ?? string.Empty,
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

    private static void ValidateSourceCode(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode) || sourceCode.Trim().Length < 5)
        {
            throw new ArgumentException("El código fuente es demasiado corto.");
        }

        if (sourceCode.Length > 200000)
        {
            throw new ArgumentException("El código fuente es demasiado largo.");
        }
    }

    private async Task<PublicSubmissionResponse> SubmitToJudgeAsync(string launchToken, IdeSubmissionRequest submission)
    {
        IdeLaunchClaims claims = ValidateLaunchToken(launchToken);
        int? contestId = ResolveContestId(claims, submission);

        return await _publicService.SubmitAsync(ToCurrentUser(claims), new PublicSubmissionRequest
        {
            ProblemId = ResolveProblemId(claims, submission),
            ContestId = contestId,
            Num = claims.Num ?? submission.Num,
            ContestProblemId = ResolveContestProblemId(claims, submission, contestId),
            CourseId = claims.CourseId,
            AssignmentId = claims.AssignmentId,
            SourceCode = submission.SourceCode,
            LanguageId = ResolveLanguageId(claims, submission),
            ClientIp = submission.ClientIp
        });
    }

    private static int? ResolveContestId(IdeLaunchClaims claims, IdeSubmissionRequest submission)
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

    private static string? ResolveContestProblemId(IdeLaunchClaims claims, IdeSubmissionRequest submission, int? contestId)
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

    private static int ResolveProblemId(IdeLaunchClaims claims, IdeSubmissionRequest submission)
    {
        int requestProblemId = submission.ProblemIdAsInt();
        int problemId = requestProblemId > 0 ? requestProblemId : claims.ProblemId;

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

    private static int ResolveLanguageId(IdeLaunchClaims claims, IdeSubmissionRequest submission)
    {
        if (!submission.LanguageId.HasValue || submission.LanguageId.Value <= 0)
        {
            throw new ArgumentException("LanguageId es requerido.");
        }

        int languageId = submission.LanguageId.Value;
        if (claims.AllowedLanguages.Length > 0 && !claims.AllowedLanguages.Contains(languageId))
        {
            throw new UnauthorizedAccessException("El token de IDE no permite usar este lenguaje.");
        }

        return languageId;
    }

    private static string ToPhase(PublicSubmissionStatusResponse status, bool isCustomInput)
    {
        if (isCustomInput && (status.ResultCode >= JudgeResultCodes.Accepted || status.ResultCode == JudgeResultCodes.TestRunDone))
        {
            return "completed";
        }

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

    private static string ToVerdict(PublicSubmissionStatusResponse status, bool isCustomInput)
    {
        if (isCustomInput && status.ResultCode == JudgeResultCodes.TestRunDone)
        {
            return "Accepted";
        }

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
