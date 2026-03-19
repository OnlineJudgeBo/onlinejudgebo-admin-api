using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Helpers;

internal static class PublicSubmissionStatusMapper
{
    public static PublicSubmissionStatusResponse Map(Solution solution)
    {
        var verdict = JudgeVerdictCatalog.Map(solution.Result);

        return new PublicSubmissionStatusResponse
        {
            SolutionId = solution.SolutionId,
            ProblemId = solution.ProblemId,
            ContestId = solution.ContestId,
            ContestProblemId = solution.ContestId.HasValue && solution.Num >= 0 ? ContestProblemCode.FromNumber(solution.Num) : null,
            UserId = solution.UserId,
            Nick = solution.User?.UserProfile?.Nick ?? solution.UserId,
            LanguageId = solution.Language,
            ResultCode = solution.Result,
            StatusKey = verdict.StatusKey,
            StatusLabel = verdict.StatusLabel,
            GeneralStatusKey = verdict.GeneralStatusKey,
            GeneralStatusLabel = verdict.GeneralStatusLabel,
            IsFinal = verdict.IsFinal,
            TimeMs = solution.Time,
            MemoryKb = solution.Memory,
            PassRate = solution.PassRate,
            CreatedAtUtc = solution.InDate,
            JudgeTimeUtc = solution.JudgeTime,
            CompileMessage = solution.Compileinfo?.Error,
            RuntimeMessage = solution.Runtimeinfo?.Error,
            SourceCode = solution.SourceCode?.Source
        };
    }
}
