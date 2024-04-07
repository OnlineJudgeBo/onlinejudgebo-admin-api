using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IProblemRepository
{
    public Task<IEnumerable<Problem>> GetAllProblemsAsync();
    public Task<Problem> GetProblemByIdAsync(int problemId);
    public Task<Problem> CreateProblemAsync(Problem problem);
    public Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem);
    public Task<Problem> DeleteProblemAsync(int problemId);
}
