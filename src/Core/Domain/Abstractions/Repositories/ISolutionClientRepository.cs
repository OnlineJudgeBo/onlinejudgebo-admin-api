namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ISolutionClientRepository
{
    public Task<int> SaveRemoteSolutionAsync(int solutionId, int clientId);
}
