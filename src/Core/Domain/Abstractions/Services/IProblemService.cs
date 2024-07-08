using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProblemService
{
    public Task<IEnumerable<Problem>> GetAllProblemsAsync(CurrentUser currentUser);
    public Task<Problem> GetProblemByIdAsync(int problemId);
    public Task<Problem> CreateProblemAsync(string userId, Problem problem);
    public Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem);
    public Task DeleteProblemAsync(int problemId);
    public Task<IEnumerable<Problem>> SearchProblemAsync(CurrentUser currentUser, string searchTerm);
}

