using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestSimilarityService
{
    Task RequestRunAsync(int contestId, int siteId);

    Task<ContestSimilarityResponse> GetAsync(int contestId, int siteId);

    // Every submission of the contest with its code, laid out to look for cheating.
    Task<byte[]> ExportSubmissionsAsync(int contestId, int siteId);
}
