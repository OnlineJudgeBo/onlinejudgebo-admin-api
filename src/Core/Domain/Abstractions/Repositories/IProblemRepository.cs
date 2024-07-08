using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IProblemRepository
{
    public Task<IEnumerable<Problem>> GetAllProblemsAsync();
    public Task<IEnumerable<Problem>> GetAllProblemsForAdminAsync();
    public Task<Problem> GetProblemByIdAsync(int problemId);
    public Task<Problem> CreateProblemAsync(Problem problem);
    public Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem);
    public Task DeleteProblemAsync(int problemId);
    public Task<IEnumerable<Problem>> SearchProblemAsync(string searchTerm);
    public Task<IEnumerable<Problem>> SearchProblemForAdminAsync(string searchTerm);
}
