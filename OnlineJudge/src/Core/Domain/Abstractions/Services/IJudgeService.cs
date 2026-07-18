using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IJudgeService
{
    public Task<RejudgeOperationResponse> RejudgeSolutionByIdAsync(int siteId, int solutionId);
    public Task<ManualJudgeResponse> ManuallyJudgeSolutionAsync(int siteId, int solutionId, short resultCode);
    public Task<RejudgeOperationResponse> RejudgeSolutionByProblemIdAsync(int siteId, int problemId);
    public Task<RejudgeOperationResponse> RejudgeSolutionByContestIdAsync(int siteId, int contestId);
    public Task<RejudgeOperationResponse> RejudgeSolutionsByRangeAsync(int siteId, int fromSolutionId, int toSolutionId);
    public Task<RejudgeOperationResponse> RejudgeSolutionsByLanguageAsync(int siteId, int languageId);
    public Task<RejudgeHistoryResponse> GetRejudgeHistoryAsync(int siteId, int limit);
    public Task RemoteExecutionAsync(RemoteExecutionRequest request, string userId, int siteId);
}
