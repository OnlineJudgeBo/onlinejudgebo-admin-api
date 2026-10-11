using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

// Reads and writes the scoring.json the judge kernel uses to score a problem by test groups.
public interface IProblemScoringService
{
    Task<ProblemScoring> GetAsync(int problemId, int siteId);

    // No groups removes the file: the problem goes back to plain accepted/rejected judging.
    Task<ProblemScoring> SaveAsync(int problemId, int siteId, ProblemScoring scoring);
}
