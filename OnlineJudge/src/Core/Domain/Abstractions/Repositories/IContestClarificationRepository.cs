using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IContestClarificationRepository
{
    // userId null: every message (staff view); otherwise the public ones plus that user's own.
    Task<IReadOnlyList<ContestClarification>> GetAsync(int contestId, int siteId, string? userId);

    Task<ContestClarification> AddAsync(ContestClarification clarification, int siteId);

    Task<ContestClarification> AnswerAsync(int contestId, int siteId, int id, string answer, bool isPublic, string answeredBy, DateTime answeredAt);

    Task<DateTime?> GetLastQuestionAtAsync(int contestId, int siteId, string userId);

    Task<int> CountPendingQuestionsAsync(int contestId, int siteId, string userId);

    Task<IReadOnlyList<ContestClarificationPendingCount>> GetPendingCountsAsync(int siteId);
}
