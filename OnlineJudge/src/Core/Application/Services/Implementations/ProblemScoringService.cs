using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Reads and writes the scoring.json the judge kernel uses to score a problem by test groups.
public sealed class ProblemScoringService(IProblemService problems, IFileSystemLocalManagerManager files) : IProblemScoringService
{
    private const string FileName = ProblemScoringRules.FileName;
    private static readonly string[] Types = ["sum", "min", "mul"];

    public async Task<ProblemScoring> GetAsync(int problemId, int siteId)
    {
        await EnsureProblemAsync(problemId, siteId);
        var folder = problemId.ToString();
        var fileNames = files.ListFiles(folder);
        var scoring = fileNames.Contains(FileName)
            ? ProblemScoringRules.Parse(files.ReadFile(folder, FileName))
            : new ProblemScoring();
        scoring.AvailableTests = ProblemScoringRules.AvailableTests(fileNames);
        return scoring;
    }

    // No groups removes the file: the problem goes back to plain accepted/rejected judging.
    public async Task<ProblemScoring> SaveAsync(int problemId, int siteId, ProblemScoring scoring)
    {
        await EnsureProblemAsync(problemId, siteId);
        var folder = problemId.ToString();
        var tests = ProblemScoringRules.AvailableTests(files.ListFiles(folder));
        var groups = Validate(scoring.Groups ?? [], tests);

        if (groups.Count == 0)
        {
            files.DeleteFile(folder, FileName);
        }
        else
        {
            files.CreateFolder(folder);
            files.WriteToFile(folder, FileName, ProblemScoringRules.Serialize(groups));
        }

        return new ProblemScoring { Groups = groups, AvailableTests = tests };
    }

    private static List<ProblemScoreGroup> Validate(IReadOnlyList<ProblemScoreGroup> groups, IReadOnlyList<string> tests)
    {
        var result = new List<ProblemScoreGroup>();
        foreach (var (group, index) in groups.Select((group, index) => (group, index)))
        {
            var label = string.IsNullOrWhiteSpace(group.Name) ? $"Grupo {index + 1}" : group.Name.Trim();
            var type = (group.Type ?? "sum").Trim().ToLowerInvariant();
            var patterns = (group.Tests ?? []).Select(pattern => pattern.Trim()).Where(pattern => pattern.Length > 0).Distinct().ToList();

            if (group.Points <= 0)
            {
                throw new ArgumentException($"{label}: los puntos deben ser mayores que 0.");
            }

            if (!Types.Contains(type))
            {
                throw new ArgumentException($"{label}: el tipo debe ser sum, min o mul.");
            }

            if (patterns.Count == 0 || !tests.Any(test => patterns.Any(pattern => ProblemScoringRules.Matches(pattern, test))))
            {
                throw new ArgumentException($"{label}: no coincide con ningún caso de prueba.");
            }

            result.Add(new ProblemScoreGroup { Name = label, Points = group.Points, Type = type, Tests = patterns });
        }

        return result;
    }

    private async Task EnsureProblemAsync(int problemId, int siteId)
    {
        _ = await problems.GetProblemByIdAsync(problemId, siteId)
            ?? throw new KeyNotFoundException("Problema no encontrado.");
    }
}
