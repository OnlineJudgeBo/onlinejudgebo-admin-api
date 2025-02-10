namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IJudgeRepository
{
    public Task RejudgeSolutionByIdAsync(int solutionId);
    public Task RejudgeSolutionByProblemIdAsync(int problemId);
}
