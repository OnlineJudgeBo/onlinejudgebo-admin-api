using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProblemService
{
    public Task<IEnumerable<Problem>> GetAllProblemsAsync(CurrentUser currentUser);
    public Task<Problem> GetProblemByIdAsync(int problemId, int? siteId = null);
    public Task<Problem> CreateProblemAsync(string userId, Problem problem, int siteId);
    public Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem, int siteId);
    public Task ChangeProblemVisibilityAsync(int problemId, int siteId);
    public Task DeleteProblemAsync(int problemId, int siteId);
    public Task<IEnumerable<Problem>> SearchProblemAsync(CurrentUser currentUser, string searchTerm);
}
