using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class PublicRepository
{
    public async Task<PublicSubmissionsResponse> GetSubmissionsAsync(int siteId, int page, int pageSize, int? contestId, int? problemId, string? userId, int? languageId, string? statusKey, CurrentUser? currentUser = null)
    {
        var baseQuery = OfficialSolutions()
            .Where(solution => solution.SiteId == siteId);

        if (contestId.HasValue)
        {
            var contest = await GetContestAsync(siteId, contestId.Value);
            await CheckContestAccessAsync(siteId, contest, currentUser);
            baseQuery = baseQuery.Where(solution => solution.ContestId == contestId.Value);
        }

        if (problemId.HasValue)
        {
            baseQuery = baseQuery.Where(solution => solution.ProblemId == problemId.Value);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var normalizedUserId = userId.Trim();
            baseQuery = baseQuery.Where(solution => solution.UserId == normalizedUserId);
        }

        if (languageId.HasValue)
        {
            baseQuery = baseQuery.Where(solution => solution.Language == languageId.Value);
        }

        var resultCodes = ResolveSubmissionStatusCodes(statusKey);
        if (resultCodes.Count > 0)
        {
            baseQuery = baseQuery.Where(solution => resultCodes.Contains(solution.Result));
        }

        var total = await baseQuery.CountAsync();

        var solutions = await baseQuery
            .OrderByDescending(solution => solution.SolutionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = siteId,
                Total = total,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.Now,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var userIds = solutions.Select(solution => solution.UserId).Distinct().ToList();
        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var nickMap = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => string.IsNullOrWhiteSpace(profile.Nick) ? profile.UserId : profile.Nick);

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        var items = solutions
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionListItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ContestId = solution.ContestId,
                    ContestProblemId = solution.ContestId.HasValue && solution.Num >= 0 ? ContestProblemCode.FromNumber(solution.Num) : null,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    UserId = solution.UserId,
                    Nick = nickMap.GetValueOrDefault(solution.UserId, solution.UserId),
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
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
                    JudgeTimeUtc = solution.Judgetime
                };
            })
            .ToList();

        return new PublicSubmissionsResponse
        {
            SiteId = siteId,
            Total = total,
            Page = page,
            PageSize = pageSize,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

    public async Task<PublicSubmissionsResponse> GetOwnSubmissionsAsync(CurrentUser currentUser, int page, int pageSize)
    {
        var baseQuery = OfficialSolutions()
            .Where(solution => solution.SiteId == currentUser.SiteId && solution.UserId == currentUser.UserId);

        var total = await baseQuery.CountAsync();

        var solutions = await baseQuery
            .OrderByDescending(solution => solution.SolutionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = currentUser.SiteId,
                Total = total,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.Now,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var nick = await _context.UserProfiles
            .Where(profile => profile.SiteId == currentUser.SiteId && profile.UserId == currentUser.UserId)
            .Select(profile => string.IsNullOrWhiteSpace(profile.Nick) ? currentUser.UserId : profile.Nick)
            .FirstOrDefaultAsync() ?? currentUser.UserId;

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        var items = solutions
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionListItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ContestId = solution.ContestId,
                    ContestProblemId = solution.ContestId.HasValue && solution.Num >= 0 ? ContestProblemCode.FromNumber(solution.Num) : null,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    UserId = solution.UserId,
                    Nick = nick,
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
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
                    JudgeTimeUtc = solution.Judgetime
                };
            })
            .ToList();

        return new PublicSubmissionsResponse
        {
            SiteId = currentUser.SiteId,
            Total = total,
            Page = page,
            PageSize = pageSize,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

    public async Task<PublicSubmissionsResponse> GetRecentSubmissionsAsync(int siteId, int page, int pageSize, int? contestId, long? courseId, CurrentUser? currentUser = null)
    {
        if (courseId.HasValue)
        {
            var courseSolutionsQuery = _academicContext.CourseSubmissionContexts
                .Where(item => item.CourseId == courseId.Value);

            var total = await courseSolutionsQuery.CountAsync();
            var solutionIds = await courseSolutionsQuery
                .OrderByDescending(item => item.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => (int)item.SolutionId)
                .ToListAsync();

            var solutionOrder = solutionIds
                .Select((solutionId, index) => new { solutionId, index })
                .ToDictionary(item => item.solutionId, item => item.index);

            var courseSolutions = await OfficialSolutions()
                .Where(solution => solution.SiteId == siteId && solutionIds.Contains(solution.SolutionId))
                .ToListAsync();

            courseSolutions = courseSolutions
                .OrderBy(solution => solutionOrder.GetValueOrDefault(solution.SolutionId, int.MaxValue))
                .ToList();

            return await BuildSubmissionsResponseAsync(siteId, total, page, pageSize, courseSolutions);
        }

        var baseQuery = OfficialSolutions()
            .Where(solution => solution.SiteId == siteId);

        if (contestId.HasValue)
        {
            var contest = await GetContestAsync(siteId, contestId.Value);
            await CheckContestAccessAsync(siteId, contest, currentUser);
            baseQuery = baseQuery.Where(solution => solution.ContestId == contestId.Value);
        }

        var submissionsTotal = await baseQuery.CountAsync();
        var solutions = await baseQuery
            .OrderByDescending(solution => solution.SolutionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return await BuildSubmissionsResponseAsync(siteId, submissionsTotal, page, pageSize, solutions);
    }

    public async Task<IReadOnlyCollection<PublicSubmissionSourceCodeItem>> GetOwnSubmissionSourceCodesAsync(CurrentUser currentUser)
    {
        var solutions = await OfficialSolutions()
            .AsNoTracking()
            .Where(solution => solution.SiteId == currentUser.SiteId && solution.UserId == currentUser.UserId)
            .Include(solution => solution.SourceCode)
            .OrderByDescending(solution => solution.SolutionId)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return Array.Empty<PublicSubmissionSourceCodeItem>();
        }

        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        return solutions
            .Where(solution => !string.IsNullOrWhiteSpace(solution.SourceCode?.Source))
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionSourceCodeItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
                    StatusKey = verdict.StatusKey,
                    CreatedAtUtc = solution.InDate,
                    SourceCode = solution.SourceCode!.Source
                };
            })
            .ToList();
    }

    public async Task<PublicSubmissionResponse> SubmitAsync(CurrentUser currentUser, PublicSubmissionRequest request, int languageId)
    {
        bool userExists = await _context.Users
            .AnyAsync(user => user.UserId == currentUser.UserId
                && user.SiteId == currentUser.SiteId
                && !user.IsDeleted
                && user.IsActive);

        if (!userExists)
        {
            throw new ArgumentException("Usuario inválido para este sitio.");
        }

        int? contestId = request.ContestId.HasValue && request.ContestId.Value > 0
            ? request.ContestId.Value
            : null;
        ContestProblemReference? contestProblem = contestId.HasValue
            ? await ResolveContestProblemAsync(currentUser.SiteId, contestId.Value, request, currentUser)
            : null;
        int resolvedProblemId = contestProblem?.ProblemId ?? request.ProblemId ?? 0;

        if (resolvedProblemId <= 0)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        bool problemExists = await _context.ProblemSites
            .AnyAsync(problemSite => problemSite.problemId == resolvedProblemId && problemSite.SiteId == currentUser.SiteId);

        if (!problemExists)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        bool languageExists = await _context.ProgrammingLanguages
            .AnyAsync(language => language.LanguageId == languageId);

        if (!languageExists)
        {
            throw new ArgumentException("LanguageId no soportado.");
        }

        if (contestId.HasValue)
        {
            DbContest contest = await GetContestAsync(currentUser.SiteId, contestId.Value);
            await CheckContestAccessAsync(currentUser.SiteId, contest, currentUser);
            CheckContestOpen(contest);
        }

        bool requiresContestProblem = contestId.HasValue
            && (!string.IsNullOrWhiteSpace(request.ContestProblemId)
                || request.Num.HasValue && request.Num.Value >= 0);

        if (requiresContestProblem && contestProblem == null)
        {
            throw new ArgumentException("Problema del concurso inválido o no disponible.");
        }

        DateTime now = DateTime.Now;

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

        DbSolution solution = new DbSolution
        {
            ProblemId = resolvedProblemId,
            UserId = currentUser.UserId,
            Time = 0,
            Memory = 0,
            InDate = now,
            Result = 0,
            Language = (uint)languageId,
            Ip = request.ClientIp,
            ContestId = request.ContestId,
            Num = contestProblem?.Num ?? -1,
            CodeLength = request.SourceCode.Length,
            PassRate = 0,
            IsRemoteOj = false,
            RemoteId = 0,
            SiteId = currentUser.SiteId
        };

        await _context.Solutions.AddAsync(solution);
        await _context.SaveChangesAsync();

        await _context.SourceCodes.AddAsync(new DbSourceCode
        {
            SolutionId = solution.SolutionId,
            Source = request.SourceCode
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new PublicSubmissionResponse
        {
            SolutionId = solution.SolutionId,
            LanguageId = languageId,
            AutoDetected = false,
            CreatedAtUtc = now
        };
    }

    private async Task<ContestProblemReference?> ResolveContestProblemAsync(int siteId, int contestId, PublicSubmissionRequest request, CurrentUser? currentUser = null)
    {
        if (!string.IsNullOrWhiteSpace(request.ContestProblemId))
        {
            return await ResolveContestProblemReferenceAsync(siteId, contestId, request.ContestProblemId, currentUser);
        }

        IQueryable<DbContestProblem> query = _context.ContestProblems
            .Where(problem => problem.ContestId == contestId
                && problem.Num.HasValue
                && problem.ProblemId.HasValue);

        if (request.Num.HasValue && request.Num.Value >= 0)
        {
            query = query.Where(problem => problem.Num == request.Num.Value);
        }
        else if (request.ProblemId.HasValue && request.ProblemId.Value > 0)
        {
            query = query.Where(problem => problem.ProblemId == request.ProblemId.Value);
        }
        else
        {
            return null;
        }

        return await query
            .Select(problem => new ContestProblemReference
            {
                ContestId = contestId,
                ProblemId = problem.ProblemId!.Value,
                Num = problem.Num!.Value,
                ContestProblemId = ContestProblemCode.FromNumber(problem.Num!.Value)
            })
            .FirstOrDefaultAsync();
    }

    private async Task<HashSet<int>> LoadSolvedProblemIdsAsync(int siteId, CurrentUser? currentUser, IEnumerable<int> problemIds, int? contestId = null)
    {
        var problemIdList = problemIds.Distinct().ToList();
        if (currentUser == null
            || string.IsNullOrWhiteSpace(currentUser.UserId)
            || currentUser.UserId == "defaultUserId"
            || problemIdList.Count == 0)
        {
            return new HashSet<int>();
        }

        var solvedProblemIds = await OfficialSolutions()
            .Where(solution =>
                solution.SiteId == siteId
                && solution.Result == AcceptedResultCode
                && solution.UserId == currentUser.UserId
                && (!contestId.HasValue || solution.ContestId == contestId.Value)
                && problemIdList.Contains(solution.ProblemId))
            .Select(solution => solution.ProblemId)
            .Distinct()
            .ToListAsync();

        return solvedProblemIds.ToHashSet();
    }

    private async Task<HashSet<int>> LoadAttemptedProblemIdsAsync(int siteId, CurrentUser? currentUser, IEnumerable<int> problemIds, int? contestId = null)
    {
        var problemIdList = problemIds.Distinct().ToList();
        if (currentUser == null
            || string.IsNullOrWhiteSpace(currentUser.UserId)
            || currentUser.UserId == "defaultUserId"
            || problemIdList.Count == 0)
        {
            return new HashSet<int>();
        }

        var attemptedProblemIds = await OfficialSolutions()
            .Where(solution =>
                solution.SiteId == siteId
                && solution.UserId == currentUser.UserId
                && (!contestId.HasValue || solution.ContestId == contestId.Value)
                && problemIdList.Contains(solution.ProblemId))
            .Select(solution => solution.ProblemId)
            .Distinct()
            .ToListAsync();

        return attemptedProblemIds.ToHashSet();
    }

    private static decimal CalculateSuccessRate(int accepted, int submit)
    {
        return submit <= 0 ? 0 : Math.Round((decimal)accepted * 100m / submit, 2);
    }

    private static string CalculateDifficulty(int accepted, int submit)
    {
        var successRate = CalculateSuccessRate(accepted, submit);
        if (successRate >= 65m)
        {
            return "EASY";
        }

        if (successRate >= 35m)
        {
            return "MEDIUM";
        }

        return "HARD";
    }

    private async Task<PublicSubmissionsResponse> BuildSubmissionsResponseAsync(
        int siteId,
        int total,
        int page,
        int pageSize,
        IReadOnlyCollection<DbSolution> solutions)
    {
        if (solutions.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = siteId,
                Total = total,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.Now,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var userIds = solutions.Select(solution => solution.UserId).Distinct().ToList();
        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var nickMap = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => string.IsNullOrWhiteSpace(profile.Nick) ? profile.UserId : profile.Nick);

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        var items = solutions
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionListItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ContestId = solution.ContestId,
                    ContestProblemId = solution.ContestId.HasValue && solution.Num >= 0 ? ContestProblemCode.FromNumber(solution.Num) : null,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    UserId = solution.UserId,
                    Nick = nickMap.GetValueOrDefault(solution.UserId, solution.UserId),
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
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
                    JudgeTimeUtc = solution.Judgetime
                };
            })
            .ToList();

        return new PublicSubmissionsResponse
        {
            SiteId = siteId,
            Total = total,
            Page = page,
            PageSize = pageSize,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

    private static IReadOnlyCollection<short> ResolveSubmissionStatusCodes(string? statusKey)
    {
        return (statusKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "queued" => new[] { JudgeResultCodes.Pending, JudgeResultCodes.WaitRejudge },
            "evaluating" => new[] { JudgeResultCodes.Compiling, JudgeResultCodes.RunningAndJudging, JudgeResultCodes.CompileOk, JudgeResultCodes.TestRunDone },
            "finished" => new[]
            {
                JudgeResultCodes.Accepted,
                JudgeResultCodes.PresentationError,
                JudgeResultCodes.WrongAnswer,
                JudgeResultCodes.TimeLimitExceeded,
                JudgeResultCodes.MemoryLimitExceeded,
                JudgeResultCodes.OutputLimitExceeded,
                JudgeResultCodes.RuntimeError,
                JudgeResultCodes.CompileError
            },
            "pending" => new[] { JudgeResultCodes.Pending },
            "pending_rejudge" => new[] { JudgeResultCodes.WaitRejudge },
            "compiling" => new[] { JudgeResultCodes.Compiling },
            "running" => new[] { JudgeResultCodes.RunningAndJudging },
            "accepted" => new[] { JudgeResultCodes.Accepted },
            "presentation_error" => new[] { JudgeResultCodes.PresentationError },
            "wrong_answer" => new[] { JudgeResultCodes.WrongAnswer },
            "time_limit_exceeded" => new[] { JudgeResultCodes.TimeLimitExceeded },
            "memory_limit_exceeded" => new[] { JudgeResultCodes.MemoryLimitExceeded },
            "output_limit_exceeded" => new[] { JudgeResultCodes.OutputLimitExceeded },
            "runtime_error" => new[] { JudgeResultCodes.RuntimeError },
            "compile_error" => new[] { JudgeResultCodes.CompileError },
            "compiled" => new[] { JudgeResultCodes.CompileOk },
            "test_run" => new[] { JudgeResultCodes.TestRunDone },
            _ => Array.Empty<short>()
        };
    }
}
