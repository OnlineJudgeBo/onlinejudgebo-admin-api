using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestSimilarityService
{
    Task RequestRunAsync(int contestId, int siteId);

    Task<ContestSimilarityResponse> GetAsync(int contestId, int siteId);
}
