using System.IO.Compression;
using System.Text.Json;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// A contest package is one manifest plus the existing lossless ZIP of every problem.
public sealed class ContestPackageService(
    IContestService contests,
    IProblemPackageService problems,
    IProgrammingLanguageService languages)
{
    private const int MaxProblems = 500;
    private const long MaxProblemPackageBytes = 200_000_000;
    private const long MaxBundleBytes = 190_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<byte[]> ExportAsync(int contestId, int siteId)
    {
        var contest = await contests.GetContestById(contestId, siteId)
            ?? throw new KeyNotFoundException("Concurso no encontrado.");
        var contestProblems = contest.ContestProblems.OrderBy(item => item.Num).ToList();
        if (contestProblems.Count > MaxProblems)
        {
            throw new InvalidOperationException($"El concurso supera el máximo de {MaxProblems} problemas por paquete.");
        }

        // ponytail: buffered ZIP; move to a temporary streamed file if real packages exceed memory limits.
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entries = new List<string>();
            long totalBytes = 0;
            foreach (var (contestProblem, index) in contestProblems.Select((item, index) => (item, index)))
            {
                var problemId = contestProblem.ProblemId
                    ?? throw new InvalidOperationException("El concurso contiene un problema sin identificador.");
                var entryName = $"problems/{index + 1:D3}-problem-{problemId}.zip";
                var bytes = await problems.ExportProblemPackageAsync(problemId, siteId);
                totalBytes += bytes.Length;
                if (totalBytes > MaxBundleBytes)
                {
                    throw new InvalidOperationException("El paquete del concurso supera el límite de 190 MB.");
                }
                var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                await using var stream = entry.Open();
                await stream.WriteAsync(bytes);
                entries.Add(entryName);
            }

            var manifest = new ContestPackageManifest
            {
                Title = contest.Title ?? $"Contest {contestId}",
                Description = contest.Description ?? string.Empty,
                StartTime = contest.StartTime,
                EndTime = contest.EndTime,
                IsPrivate = contest.Private != 0,
                IsOfficial = contest.Defunct == "O",
                Track = contest.Track,
                Level = contest.Level,
                IsExam = contest.IsExam,
                LanguageNames = contest.ProgrammingLanguages?
                    .Select(item => item.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToList() ?? [],
                Problems = entries,
            };
            var manifestEntry = archive.CreateEntry("contest.json", CompressionLevel.Optimal);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions);
        }
        return output.ToArray();
    }

    public async Task<Contest> ImportAsync(string userId, Stream packageStream, int siteId)
    {
        using var copy = new MemoryStream();
        await packageStream.CopyToAsync(copy);
        copy.Position = 0;
        using var archive = new ZipArchive(copy, ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("contest.json")
            ?? throw new InvalidDataException("El paquete no contiene contest.json.");
        if (manifestEntry.Length is <= 0 or > 1_000_000)
        {
            throw new InvalidDataException("contest.json no es válido.");
        }
        ContestPackageManifest manifest;
        await using (var stream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<ContestPackageManifest>(stream, JsonOptions)
                ?? throw new InvalidDataException("contest.json no es válido.");
        }
        Validate(manifest, archive);

        var importedProblems = new List<ContestProblem>();
        foreach (var (entryName, index) in manifest.Problems.Select((name, index) => (name, index)))
        {
            var entry = archive.GetEntry(entryName)!;
            await using var stream = entry.Open();
            var problem = await problems.ImportProblemPackageAsync(userId, stream, siteId);
            importedProblems.Add(new ContestProblem { ProblemId = problem.ProblemId, Num = index });
        }

        var languageNames = manifest.LanguageNames ?? [];
        var availableLanguages = await languages.GetAllProgrammingLanguageAsync();
        var selectedLanguages = availableLanguages
            .Where(language => languageNames.Contains(language.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var contest = new Contest
        {
            Title = manifest.Title.Trim(),
            Description = manifest.Description,
            StartTime = manifest.StartTime,
            EndTime = manifest.EndTime,
            Private = manifest.IsPrivate ? (sbyte)1 : (sbyte)0,
            Defunct = manifest.IsOfficial ? "O" : "N",
            Track = manifest.Track ?? "GENERAL",
            Level = manifest.Level ?? "PRACTICE",
            IsExam = manifest.IsExam,
            ContestProblems = importedProblems,
            ContestUsers = [],
            ProgrammingLanguages = selectedLanguages,
        };
        return await contests.CreateContestAsync(userId, contest, string.Empty, siteId);
    }

    private static void Validate(ContestPackageManifest manifest, ZipArchive archive)
    {
        if (manifest.Version != 1 || string.IsNullOrWhiteSpace(manifest.Title) || manifest.Title.Length > 255
            || manifest.StartTime == default || manifest.EndTime < manifest.StartTime || manifest.Problems == null)
        {
            throw new InvalidDataException("contest.json no es válido.");
        }
        if (manifest.Problems.Count > MaxProblems || manifest.Problems.Distinct(StringComparer.Ordinal).Count() != manifest.Problems.Count)
        {
            throw new InvalidDataException($"El paquete debe contener como máximo {MaxProblems} problemas sin duplicados.");
        }
        foreach (var name in manifest.Problems)
        {
            var entry = name.StartsWith("problems/", StringComparison.Ordinal)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? archive.GetEntry(name)
                : null;
            if (entry == null || entry.Length <= 0 || entry.Length > MaxProblemPackageBytes)
            {
                throw new InvalidDataException($"Paquete de problema inválido: {name}");
            }
        }
        if (manifest.Problems.Sum(name => archive.GetEntry(name)!.Length) > MaxBundleBytes)
        {
            throw new InvalidDataException("El paquete del concurso supera el límite de 190 MB.");
        }
    }
}

internal sealed class ContestPackageManifest
{
    public int Version { get; init; } = 1;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsOfficial { get; init; }
    public string Track { get; init; } = "GENERAL";
    public string Level { get; init; } = "PRACTICE";
    public bool IsExam { get; init; }
    public List<string> LanguageNames { get; init; } = [];
    public List<string> Problems { get; init; } = [];
}
