using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestClarificationService
{
    Task<IReadOnlyList<ContestClarification>> GetAllAsync(int contestId, int siteId);

    Task<IReadOnlyList<ContestClarificationPendingCount>> GetPendingCountsAsync(int siteId);

    Task<ContestClarification> AnswerAsync(int contestId, int id, string answer, bool isPublic, CurrentUser currentUser);

    Task<ContestClarification> AnnounceAsync(int contestId, int? problemNum, string text, CurrentUser currentUser);

    Task<IReadOnlyList<ContestClarification>> GetForParticipantAsync(int siteId, int contestId, CurrentUser? currentUser);

    Task<ContestClarification> AskAsync(int siteId, int contestId, int? problemNum, string question, CurrentUser currentUser);
}
