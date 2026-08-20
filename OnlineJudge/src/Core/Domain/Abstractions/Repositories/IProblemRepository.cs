using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IProblemRepository
{
    public Task<IEnumerable<Problem>> GetAllProblemsAsync(int siteId);
    public Task<IEnumerable<Problem>> GetAllProblemsForAdminAsync(int siteId);
    public Task<Problem> GetProblemByIdAsync(int problemId, int? siteId = null);
    public Task<Problem> CreateProblemAsync(Problem problem, int siteId);
    public Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem, int siteId);
    public Task PromoteProblemAsync(List<int> problemId);
    public Task ChangeProblemVisibilityAsync(int problemId, int siteId);
    public Task DeleteProblemAsync(int problemId, int siteId);
    public Task<IEnumerable<Problem>> SearchProblemAsync(string searchTerm, int siteId);
    public Task<IEnumerable<Problem>> SearchProblemForAdminAsync(string searchTerm, int siteId);
}
