using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed class IdeSubmissionService : IIdeSubmissionService
{
    private readonly IIdeLaunchTokenValidator _tokenValidator;
    private readonly IPublicService _publicService;
    private readonly AppDbContext? _dbContext;

    public IdeSubmissionService(
        IIdeLaunchTokenValidator tokenValidator,
        IPublicService publicService)
        : this(tokenValidator, publicService, null)
    {
    }

    public IdeSubmissionService(
        IIdeLaunchTokenValidator tokenValidator,
        IPublicService publicService,
        AppDbContext? dbContext)
    {
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
        _dbContext = dbContext;
    }

    public async Task<VibeSubmissionResponse> SubmitAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        PublicSubmissionResponse response = await SubmitToJudgeAsync(launchToken, submission);
        string id = response.SolutionId.ToString();

        return new VibeSubmissionResponse
        {
            SubmissionId = id,
            Id = id,
            StatusUrl = $"/api/vibe/submissions/{id}"
        };
    }

    public Task<VibeRunResponse> RunAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        return CustomInputAsync(launchToken, submission);
    }

    public async Task<VibeRunResponse> CustomInputAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        AppDbContext dbContext = _dbContext ?? throw new InvalidOperationException("AppDbContext es requerido para custom_input.");
        IdeLaunchClaims claims = ValidateLaunchToken(launchToken);
        ValidateSourceCode(submission.SourceCode);
        int? contestId = ResolveContestId(claims, submission);
        _ = ResolveContestProblemId(claims, submission, contestId);
        int problemId = ResolveProblemId(claims, submission);
        int languageId = ResolveLanguageId(claims, submission);

        await ValidateCustomInputTargetAsync(dbContext, claims, problemId, languageId);

        DateTime now = DateTime.Now;
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();

        DbSolution solution = new DbSolution
        {
            ProblemId = problemId,
            UserId = claims.UserId,
            Time = 0,
            Memory = 0,
            InDate = now,
            Result = 0,
            Language = (uint)languageId,
            Ip = "0.0.0.0",
            ContestId = contestId,
            Num = claims.Num ?? submission.Num ?? -1,
            CodeLength = submission.SourceCode.Length,
            PassRate = 0,
            IsRemoteOj = false,
            RemoteId = 0,
            SiteId = claims.SiteId
        };

        await dbContext.Solutions.AddAsync(solution);
        await dbContext.SaveChangesAsync();

        await dbContext.SourceCodes.AddAsync(new DbSourceCode
        {
            SolutionId = solution.SolutionId,
            Source = submission.SourceCode
        });

        await dbContext.CustomInputs.AddAsync(new DbCustomInput
        {
            SolutionId = solution.SolutionId,
            ProblemId = problemId,
            UserId = claims.UserId,
            SiteId = claims.SiteId,
            CreatedAt = now
        });

        foreach (DbCustomInputCase customCase in BuildCustomInputCases(solution.SolutionId, submission))
        {
            await dbContext.CustomInputCases.AddAsync(customCase);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        string id = solution.SolutionId.ToString();
        return new VibeRunResponse
        {
            RunId = id,
            Id = id,
            StatusUrl = $"/api/vibe/runs/{id}"
        };
    }

    public async Task<VibeSubmissionStatusResponse> GetStatusAsync(string launchToken, int solutionId)
    {
        IdeLaunchClaims claims = ValidateLaunchToken(launchToken);
        PublicSubmissionStatusResponse status = await _publicService.GetSubmissionStatusAsync(ToCurrentUser(claims), solutionId);
        bool isCustomInput = await IsCustomInputAsync(solutionId);
        string id = status.SolutionId.ToString();

        return new VibeSubmissionStatusResponse
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

    private async Task<bool> IsCustomInputAsync(int solutionId)
    {
        return _dbContext != null && await _dbContext.CustomInputs.AnyAsync(item => item.SolutionId == solutionId);
    }

    private static IReadOnlyCollection<DbCustomInputCase> BuildCustomInputCases(int solutionId, VibeSubmissionForCreation submission)
    {
        var cases = submission.Testcases
            .Where(item => !string.IsNullOrEmpty(item.Input) || !string.IsNullOrEmpty(item.ExpectedOutput))
            .Select((item, index) => new DbCustomInputCase
            {
                SolutionId = solutionId,
                CaseNumber = index + 1,
                InputText = item.Input ?? string.Empty,
                ExpectedOutput = item.ExpectedOutput
            })
            .ToList();

        if (cases.Count > 0)
        {
            return cases;
        }

        return new[]
        {
            new DbCustomInputCase
            {
                SolutionId = solutionId,
                CaseNumber = 1,
                InputText = submission.Stdin ?? string.Empty,
                ExpectedOutput = null
            }
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

    private static async Task ValidateCustomInputTargetAsync(AppDbContext dbContext, IdeLaunchClaims claims, int problemId, int languageId)
    {
        bool userExists = await dbContext.Users.AnyAsync(user => user.UserId == claims.UserId && user.SiteId == claims.SiteId && !user.IsDeleted && user.IsActive);
        if (!userExists)
        {
            throw new ArgumentException("Usuario inválido para este sitio.");
        }

        bool problemExists = await dbContext.ProblemSites.AnyAsync(problemSite => problemSite.problemId == problemId && problemSite.SiteId == claims.SiteId && problemSite.IsActive);
        if (!problemExists)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        bool languageExists = await dbContext.ProgrammingLanguages.AnyAsync(language => language.LanguageId == languageId);
        if (!languageExists)
        {
            throw new ArgumentException("LanguageId no soportado.");
        }
    }

    private async Task<PublicSubmissionResponse> SubmitToJudgeAsync(string launchToken, VibeSubmissionForCreation submission)
    {
        IdeLaunchClaims claims = ValidateLaunchToken(launchToken);
        int? contestId = ResolveContestId(claims, submission);

        return await _publicService.SubmitAsync(ToCurrentUser(claims), new PublicSubmissionRequest
        {
            ProblemId = ResolveProblemId(claims, submission),
            ContestId = contestId,
            Num = claims.Num ?? submission.Num,
            ContestProblemId = ResolveContestProblemId(claims, submission, contestId),
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

    private static int ResolveLanguageId(IdeLaunchClaims claims, VibeSubmissionForCreation submission)
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
