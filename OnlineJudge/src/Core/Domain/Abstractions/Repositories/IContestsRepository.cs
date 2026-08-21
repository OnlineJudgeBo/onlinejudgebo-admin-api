using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IContestsRepository
{
    public Task<IEnumerable<Contest>> GetContestsByUserIdDocenteRoleAsync(string userId, bool showAllContest, int siteId, bool includePromoted = false);
    public Task<IEnumerable<Contest>> GetContestsByAuxiliarRoleAsync(string userId, int siteId, bool includePromoted = false);
    public Task<Contest> CreateContestAsync(Contest contest, int site_id);
    public Task<Contest> GetContestByIdAsync(int contestId, int siteId);
    public Task<Contest> UpdateContestAsync(int contestId, Contest contest, int siteId);
    public Task<Contest> PromoteContestAsync(int contestId, int siteId);
}
