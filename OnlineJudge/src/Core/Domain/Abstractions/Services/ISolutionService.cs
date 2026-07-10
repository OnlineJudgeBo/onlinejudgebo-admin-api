using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface ISolutionService
{
    public Task<int> SaveSolutionRemoteAsync(string userId, int problemId, int languageId, string source, int clientSubmitId, string clientIp = "0.0.0.0");
    public Task<Solution?> GetSolutionByIdAsync(int solutionId);
    public Task UpdateSolutionRemoteAsync(Solution solution);
    public Task<AdminSubmissionAuditResponse> GetSubmissionAuditAsync(int siteId, int page, int pageSize, int? problemId, string? userId, string? clientIp);
}
