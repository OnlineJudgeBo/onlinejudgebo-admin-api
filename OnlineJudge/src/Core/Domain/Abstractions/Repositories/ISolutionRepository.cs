using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ISolutionRepository
{
    public Task<int> SaveSolutionAsync(Solution solution);
    public Task<Solution?> GetSolutionByIdAsync(int solutionId);
    public Task UpdateSolutionRemoteAsync(Solution solutionToCreate);
    public Task<AdminSubmissionAuditResponse> GetSubmissionAuditAsync(int siteId, int page, int pageSize, int? problemId, string? userId, string? clientIp);
}
