using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IJudgeService
{
    public Task RejudgeSolutionByIdAsync(int solutionId);
    public Task RejudgeSolutionByProblemIdAsync(int problemId);
    public Task RemoteExecutionAsync(RemoteExecutionRequest request, string userId, int siteId);
}
