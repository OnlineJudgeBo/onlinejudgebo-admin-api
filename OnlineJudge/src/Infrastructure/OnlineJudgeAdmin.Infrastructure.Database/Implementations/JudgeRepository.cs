using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class JudgeRepository : IJudgeRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public JudgeRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<int> RejudgeSolutionByIdAsync(int siteId, int solutionId)
    {
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE `solution` SET result = 1 WHERE site_id = {0} AND solution_id = {1}", siteId, solutionId);
    }

    public async Task<int> ManuallyJudgeSolutionAsync(int siteId, int solutionId, short resultCode)
    {
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE `solution` SET result = {0}, judgetime = CURRENT_TIMESTAMP WHERE site_id = {1} AND solution_id = {2}",
            resultCode,
            siteId,
            solutionId);
    }

    public async Task<int> RejudgeSolutionByProblemIdAsync(int siteId, int problemId)
    {
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE `solution` SET result = 1 WHERE site_id = {0} AND problem_id = {1}", siteId, problemId);
    }

    public async Task<int> RejudgeSolutionByContestIdAsync(int siteId, int contestId)
    {
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE `solution` SET result = 1 WHERE site_id = {0} AND contest_id = {1}", siteId, contestId);
    }

    public async Task<int> RejudgeSolutionsByRangeAsync(int siteId, int fromSolutionId, int toSolutionId)
    {
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE `solution` SET result = 1 WHERE site_id = {0} AND solution_id BETWEEN {1} AND {2}",
            siteId,
            fromSolutionId,
            toSolutionId);
    }

    public async Task<int> RejudgeSolutionsByLanguageAsync(int siteId, int languageId)
    {
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE `solution` SET result = 1 WHERE site_id = {0} AND language = {1}", siteId, languageId);
    }

    public async Task<RejudgeHistoryResponse> GetRejudgeHistoryAsync(int siteId, int limit)
    {
        var items = await _context.Solutions
            .Where(solution => solution.SiteId == siteId && solution.Result == JudgeResultCodes.WaitRejudge)
            .OrderByDescending(solution => solution.SolutionId)
            .Take(limit)
            .Select(solution => new RejudgeHistoryItem
            {
                SolutionId = solution.SolutionId,
                ProblemId = solution.ProblemId,
                ContestId = solution.ContestId,
                UserId = solution.UserId,
                LanguageId = (int)solution.Language,
                CreatedAtUtc = solution.InDate
            })
            .ToListAsync();

        var total = await _context.Solutions
            .CountAsync(solution => solution.SiteId == siteId && solution.Result == JudgeResultCodes.WaitRejudge);

        return new RejudgeHistoryResponse
        {
            SiteId = siteId,
            Total = total,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }
}
