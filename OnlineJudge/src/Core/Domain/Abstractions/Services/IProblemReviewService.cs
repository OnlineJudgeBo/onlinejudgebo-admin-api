using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProblemReviewService
{
    // What every problem is missing. A null site reviews the problems of every site.
    Task<List<ProblemReview>> ReviewAsync(int? siteId);
}
