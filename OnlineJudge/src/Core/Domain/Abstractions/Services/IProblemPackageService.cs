namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProblemPackageService
{
    Task<byte[]> ExportProblemPackageAsync(int problemId, int siteId);
    Task<OnlineJudgeAdmin.Core.Domain.Models.Problem> ImportProblemPackageAsync(string userId, Stream zipStream, int siteId);
}
