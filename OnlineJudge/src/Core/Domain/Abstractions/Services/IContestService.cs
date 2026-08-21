using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestService
{
    public Task<IEnumerable<Contest>> GetAllContestAsync(CurrentUser userContextRole, bool includePromoted = false);
    public Task<Contest> CreateContestAsync(string userId, Contest contest, string ManualUserList, int siteId);
    public Task<Contest> GetContestById(int contestId, int siteId);
    public Task<Contest> UpdateContestAsync(int contestId, Contest contest, string ManualUserList, int siteId);
    public Task PromoteContestAsync(int contestId, int siteId);
}
