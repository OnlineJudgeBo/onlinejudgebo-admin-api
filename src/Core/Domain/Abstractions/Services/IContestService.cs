using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestService
{
    public Task<IEnumerable<Contest>> GetAllContestAsync();
}

