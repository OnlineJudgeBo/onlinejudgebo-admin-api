using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IContestsRepository
{
    public Task<IEnumerable<Contest>> GetAllContestsAsync();
}
