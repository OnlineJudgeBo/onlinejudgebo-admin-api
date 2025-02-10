using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface ISolutionRepository
{
    public Task<int> SaveSolutionAsync(Solution solution);
    public Task<Solution> GetSolutionByIdAsync(int solutionId);
    public Task UpdateSolutionRemoteAsync(Solution solutionToCreate);
}
