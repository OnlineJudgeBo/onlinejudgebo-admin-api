using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public class SolutionRepository : ISolutionRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public SolutionRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Solution?> GetSolutionByIdAsync(int solutionId)
    {
        var solution = await _context.Solutions
            .AsNoTracking()
            .Include(item => item.Compileinfo)
            .Include(item => item.Runtimeinfo)
            .Include(item => item.SourceCode)
            .Include(item => item.User)
            .ThenInclude(item => item!.UserProfile)
            .FirstOrDefaultAsync(item => item.SolutionId == solutionId);

        return _mapper.Map<Solution?>(solution);
    }

    public async Task<int> SaveSolutionAsync(Solution solutionToCreate)
    {
        DbSolution solution = _mapper.Map<DbSolution>(solutionToCreate);
        await _context.Solutions.AddAsync(solution);
        await _context.SaveChangesAsync();
        return solution.SolutionId;
    }

    public async Task UpdateSolutionRemoteAsync(Solution solutionToCreate)
    {
        DbSolution solution = _mapper.Map<DbSolution>(solutionToCreate);
        _context.Solutions.Attach(solution);
        _context.Entry(solution).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        _context.Entry(solution).State = EntityState.Detached;
    }


    public async Task<AdminSubmissionAuditResponse> GetSubmissionAuditAsync(
        int siteId,
        int page,
        int pageSize,
        int? problemId,
        string? userId,
        string? clientIp)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Min(200, Math.Max(1, pageSize));

        var baseQuery = _context.Solutions
            .AsNoTracking()
            .Where(solution => solution.SiteId == siteId);

        if (problemId.HasValue)
        {
            baseQuery = baseQuery.Where(solution => solution.ProblemId == problemId.Value);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var normalizedUserId = userId.Trim();
            baseQuery = baseQuery.Where(solution => solution.UserId == normalizedUserId);
        }

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            var normalizedClientIp = clientIp.Trim();
            baseQuery = baseQuery.Where(solution => solution.Ip != null && solution.Ip.Contains(normalizedClientIp));
        }

        var total = await baseQuery.CountAsync();
        var solutions = await baseQuery
            .OrderByDescending(solution => solution.SolutionId)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return new AdminSubmissionAuditResponse
            {
                SiteId = siteId,
                Total = total,
                Page = safePage,
                PageSize = safePageSize,
                UpdatedAtUtc = DateTime.Now,
                Items = Array.Empty<AdminSubmissionAuditItem>()
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

                return new AdminSubmissionAuditItem
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
                    ClientIp = string.IsNullOrWhiteSpace(solution.Ip) ? "0.0.0.0" : solution.Ip,
                    CreatedAtUtc = solution.InDate,
                    JudgeTimeUtc = solution.Judgetime
                };
            })
            .ToList();

        return new AdminSubmissionAuditResponse
        {
            SiteId = siteId,
            Total = total,
            Page = safePage,
            PageSize = safePageSize,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

}
