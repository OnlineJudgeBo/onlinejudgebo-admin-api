using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class ContestClarificationRepository : IContestClarificationRepository
{
    private readonly AppDbContext _context;

    public ContestClarificationRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ContestClarification>> GetAsync(int contestId, int siteId, string? userId)
    {
        var query = _context.ContestClarifications.AsNoTracking()
            .Where(item => item.ContestId == contestId && item.SiteId == siteId);
        if (userId != null)
        {
            query = query.Where(item => item.IsPublic || item.UserId == userId);
        }

        // Newest first, pending questions on top.
        var rows = await query
            .OrderBy(item => item.Answer != null)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
        return rows.Select(ToModel).ToList();
    }

    public async Task<ContestClarification> AddAsync(ContestClarification clarification, int siteId)
    {
        var row = new DbContestClarification
        {
            ContestId = clarification.ContestId,
            SiteId = siteId,
            UserId = clarification.UserId,
            ProblemNum = clarification.ProblemNum,
            Question = clarification.Question,
            Answer = clarification.Answer,
            IsPublic = clarification.IsPublic,
            AnsweredBy = clarification.AnsweredBy,
            CreatedAt = clarification.CreatedAt,
            AnsweredAt = clarification.AnsweredAt
        };
        _context.ContestClarifications.Add(row);
        await _context.SaveChangesAsync();
        return ToModel(row);
    }

    public async Task<ContestClarification> AnswerAsync(int contestId, int siteId, int id, string answer, bool isPublic, string answeredBy, DateTime answeredAt)
    {
        var row = await _context.ContestClarifications
            .FirstOrDefaultAsync(item => item.Id == id && item.ContestId == contestId && item.SiteId == siteId)
            ?? throw new KeyNotFoundException($"No se encontró la aclaración {id}.");

        row.Answer = answer;
        row.IsPublic = isPublic;
        row.AnsweredBy = answeredBy;
        row.AnsweredAt = answeredAt;
        await _context.SaveChangesAsync();
        return ToModel(row);
    }

    public Task<DateTime?> GetLastQuestionAtAsync(int contestId, int siteId, string userId)
    {
        return _context.ContestClarifications.AsNoTracking()
            .Where(item => item.ContestId == contestId && item.SiteId == siteId && item.UserId == userId)
            .MaxAsync(item => (DateTime?)item.CreatedAt);
    }

    public async Task<IReadOnlyList<ContestClarificationPendingCount>> GetPendingCountsAsync(int siteId)
    {
        return await _context.ContestClarifications.AsNoTracking()
            .Where(item => item.SiteId == siteId && item.UserId != null && item.Answer == null)
            .GroupBy(item => item.ContestId)
            .Select(group => new ContestClarificationPendingCount { ContestId = group.Key, Pending = group.Count() })
            .ToListAsync();
    }

    private static ContestClarification ToModel(DbContestClarification row) => new()
    {
        Id = row.Id,
        ContestId = row.ContestId,
        UserId = row.UserId,
        ProblemNum = row.ProblemNum,
        Question = row.Question,
        Answer = row.Answer,
        IsPublic = row.IsPublic,
        AnsweredBy = row.AnsweredBy,
        CreatedAt = row.CreatedAt,
        AnsweredAt = row.AnsweredAt
    };
}
