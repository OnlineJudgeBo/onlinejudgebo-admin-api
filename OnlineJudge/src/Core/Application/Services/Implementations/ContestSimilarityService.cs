using System.Globalization;
using System.IO.Compression;
using System.Text;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Similarity is computed by the judge kernel (Dolos). This only leaves the request file that
// judged watches in the shared data folder and reads what the kernel wrote to similar_code.
public sealed class ContestSimilarityService(IContestsRepository contests, IFileSystemLocalManagerManager files) : IContestSimilarityService
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

    public async Task<byte[]> ExportSubmissionsAsync(int contestId, int siteId)
    {
        await EnsureContestAsync(contestId, siteId);
        var submissions = await contests.GetSubmissionsForExportAsync(contestId, siteId);

        // ponytail: buffered ZIP; stream to a temporary file if a contest outgrows memory.
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var csv = new StringBuilder("solution_id,user_id,nick,problem,problem_id,language,result,pass_rate,time_ms,memory_kb,ip,submitted_at,file\n");
            foreach (var item in submissions)
            {
                var user = SafeName(item.UserId);
                var problem = item.ProblemNum >= 0 ? ContestProblemCode.FromNumber(item.ProblemNum) : $"P{item.ProblemId}";
                var status = JudgeVerdictCatalog.Map(item.Result).StatusKey;
                var extension = SourceFileExtension.FromLanguageName(item.LanguageName);
                var allPath = $"users/{user}/all/{problem}-{item.SolutionId}-{status}{extension}";
                await AddTextAsync(archive, allPath, item.Source);
                if (item.Result == JudgeResultCodes.Accepted)
                {
                    await AddTextAsync(archive, $"users/{user}/accepted/{problem}-{item.SolutionId}{extension}", item.Source);
                    // Side by side per problem, to compare the accepted codes of different users.
                    await AddTextAsync(archive, $"problems/{problem}/{user}-{item.SolutionId}{extension}", item.Source);
                }
                csv.AppendJoin(',',
                    item.SolutionId, Csv(item.UserId), Csv(item.Nick), problem, item.ProblemId, Csv(item.LanguageName), status,
                    item.PassRate.ToString(CultureInfo.InvariantCulture), item.TimeMs, item.MemoryKb, Csv(item.Ip),
                    item.InDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), allPath).Append('\n');
            }
            await AddTextAsync(archive, "submissions.csv", csv.ToString());
        }
        return output.ToArray();
    }

    private static async Task AddTextAsync(ZipArchive archive, string path, string text)
    {
        await using var stream = archive.CreateEntry(path, CompressionLevel.Optimal).Open();
        await stream.WriteAsync(new UTF8Encoding(false).GetBytes(text));
    }

    // Old user ids may hold characters that are not valid in a folder name.
    private static string SafeName(string value) =>
        new(value.Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_').ToArray());

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";

    private async Task EnsureContestAsync(int contestId, int siteId)
    {
        _ = await contests.GetContestByIdAsync(contestId, siteId)
            ?? throw new KeyNotFoundException("Concurso no encontrado.");
    }

    private static string Folder(int contestId) => Path.Combine("contests", contestId.ToString());
}
