using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProblemClassifierService
{
    // Never persists anything -- the caller decides which of the suggested
    // Classifications to keep, through the same Problem.Classifications field every
    // other problem write (create, update, BOCA import) already goes through.
    Task<ProblemClassificationSuggestion> SuggestClassificationsAsync(Problem problem);
}
