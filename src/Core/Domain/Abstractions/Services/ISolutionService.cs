namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface ISolutionService
{
    public Task<int> SaveSolutionRemoteAsync(string userId, int problemId, int languageId, string source, int clientSubmitId);
}
