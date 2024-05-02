using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IContestsRepository
{
    public Task<IEnumerable<Contest>> GetContestsByUserIdDocenteRoleAsync(string userId, bool showAllContest);
    public Task<IEnumerable<Contest>> GetContestsByAuxiliarRoleAsync(string userId);
    public Task<Contest> CreateContestAsync(Contest contest);
    public Task<Contest> GetContestByIdAsync(int contestId);
    public Task<Contest> UpdateContestAsync(int contestId, Contest contest);
}
