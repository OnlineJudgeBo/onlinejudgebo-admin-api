namespace OnlineJudgeAdmin.Core.Domain.Models;

// Two accepted solutions of different users that the judge kernel found similar.
public sealed record ContestSimilarityItem(
    int ProblemId,
    int ProblemNum,
    int SolutionId,
    string UserId,
    int SimilarSolutionId,
    string SimilarUserId,
    double Percentage);

public sealed class ContestSimilarityResponse
{
    // A run was requested and the kernel has not finished it yet.
    public bool Running { get; init; }

    // Time and reasons of the last run that failed, as the kernel wrote them; null when it went fine.
    public string? Error { get; init; }

    public IReadOnlyCollection<ContestSimilarityItem> Items { get; init; } = Array.Empty<ContestSimilarityItem>();
}
