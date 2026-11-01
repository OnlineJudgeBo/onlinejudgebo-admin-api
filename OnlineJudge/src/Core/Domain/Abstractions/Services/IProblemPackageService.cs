namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProblemPackageService
{
    Task<byte[]> ExportProblemPackageAsync(int problemId, int siteId);
    // Imports a package as a new problem, or over problemId replacing its statement and data (KeyNotFoundException when
    // problemId is not in this site).
    Task<OnlineJudgeAdmin.Core.Domain.Models.Problem> ImportProblemPackageAsync(string userId, Stream zipStream, int siteId, int? problemId = null);
}
