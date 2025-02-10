namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface ISolutionClientRepository
{
    public Task<int> SaveRemoteSolutionAsync(int solutionId, int clientId);
    public Task SaveSourceCodeAsync(int solutionId, string sourceCode);

}
