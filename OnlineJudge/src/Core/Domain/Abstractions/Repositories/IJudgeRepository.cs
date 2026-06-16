using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IJudgeRepository
{
    public Task<int> RejudgeSolutionByIdAsync(int siteId, int solutionId);
    public Task<int> RejudgeSolutionByProblemIdAsync(int siteId, int problemId);
    public Task<int> RejudgeSolutionByContestIdAsync(int siteId, int contestId);
    public Task<int> RejudgeSolutionsByRangeAsync(int siteId, int fromSolutionId, int toSolutionId);
    public Task<int> RejudgeSolutionsByLanguageAsync(int siteId, int languageId);
    public Task<RejudgeHistoryResponse> GetRejudgeHistoryAsync(int siteId, int limit);
}
