using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

// A whole contest as a zip: its settings and one problem package per problem.
public interface IContestPackageService
{
    Task<byte[]> ExportAsync(int contestId, int siteId);

    Task<Contest> ImportAsync(string userId, Stream packageStream, int siteId);
}
