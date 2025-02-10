using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ISolutionRepository
{
    public Task<int> SaveSolutionAsync(Solution solution);
    public Task<Solution> GetSolutionByIdAsync(int solutionId);
    public Task UpdateSolutionRemoteAsync(Solution solutionToCreate);
}
