using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestService
{
    public Task<IEnumerable<Contest>> GetAllContestAsync();
    public Task<Contest> CreateContestAsync(string userId, Contest contest);
    public Task<Contest> GetContestById(int contestId);
}

