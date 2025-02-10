using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface IProblemService
{
    public Task<IEnumerable<Problem>> GetAllProblemsAsync(CurrentUser currentUser);
    public Task<Problem> GetProblemByIdAsync(int problemId);
    public Task<Problem> CreateProblemAsync(string userId, Problem problem, int siteId);
    public Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem);
    public Task ChangeProblemVisibilityAsync(int problemId);
    public Task DeleteProblemAsync(int problemId, int siteId);
    public Task<IEnumerable<Problem>> SearchProblemAsync(CurrentUser currentUser, string searchTerm);
}

