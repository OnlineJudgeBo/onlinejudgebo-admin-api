namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IJudgeService
{
    public Task RejudgeSolutionByIdAsync(int solutionId);
    public Task RejudgeSolutionByProblemIdAsync(int problemId);
}
