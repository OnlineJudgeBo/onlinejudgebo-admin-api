using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Similarity is computed by the judge kernel (Dolos). This only leaves the request file that
// judged watches in the shared data folder and reads what the kernel wrote to similar_code.
public sealed class ContestSimilarityService(IContestsRepository contests, IFileSystemLocalManagerManager files)
{
    private const string RequestFile = "similarity.request";

    public async Task RequestRunAsync(int contestId, int siteId)
    {
        await EnsureContestAsync(contestId, siteId);
        files.CreateFolder(Folder(contestId));
        files.WriteToFile(Folder(contestId), RequestFile, string.Empty);
    }

    public async Task<ContestSimilarityResponse> GetAsync(int contestId, int siteId)
    {
        await EnsureContestAsync(contestId, siteId);
        var rows = await contests.GetSimilarityAsync(contestId, siteId);
        return new ContestSimilarityResponse
        {
            Running = files.ListFiles(Folder(contestId)).Contains(RequestFile),
            // A whole-contest run stores both directions of a pair; report each pair once.
            Items = rows
                .GroupBy(item => (Math.Min(item.SolutionId, item.SimilarSolutionId), Math.Max(item.SolutionId, item.SimilarSolutionId)))
                .Select(pair => pair.MaxBy(item => item.Percentage)!)
                .OrderByDescending(item => item.Percentage)
                .ThenBy(item => item.ProblemNum)
                .ToList(),
        };
    }

    private async Task EnsureContestAsync(int contestId, int siteId)
    {
        _ = await contests.GetContestByIdAsync(contestId, siteId)
            ?? throw new KeyNotFoundException("Concurso no encontrado.");
    }

    private static string Folder(int contestId) => Path.Combine("contests", contestId.ToString());
}
