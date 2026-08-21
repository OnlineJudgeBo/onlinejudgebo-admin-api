using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnlineJudgeAdmin.BocaImporter;
using OnlineJudgeAdmin.Core.Application.Services.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Validators.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager.DependencyInjection;

const string TemplateFolderName = "problemtemplate";
// Matches the volume added to the patito-api service in docker-compose.yml - same
// destination the admin UI's /boca-import/confirm archives into.
const string ArchiveRoot = "/boca-archive";

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Console.Error.WriteLine("Usage: dotnet run -- --user-id <id> --site-id <id> [--dir <path>] [--dry-run] [package.zip | folder ...]");
    return 1;
}

var builder = Host.CreateApplicationBuilder();
builder.Services.AddDatabaseRepositories(builder.Configuration);
builder.Services.AddApplicationValidators();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddFileSystemLocalManagerInfrastructureManager(builder.Configuration);

using var host = builder.Build();
var problemService = host.Services.GetRequiredService<IProblemService>();
var fileManager = host.Services.GetRequiredService<IFileSystemLocalManagerManager>();

var targets = ResolveTargets(options);

if (targets.Count == 0)
{
    Console.WriteLine($"No .zip packages or problem folders found under {options.BocaDir} (besides {TemplateFolderName}).");
    return 0;
}

var summary = new List<(string Name, string Status, string? Detail, bool NeedsReview)>();

foreach (var target in targets)
{
    Console.WriteLine($"\n== {target.Name} ==");
    string? tempExtractRoot = null;

    try
    {
        var problemDir = target.ZipPath is null
            ? target.Path
            : (tempExtractRoot = ExtractZip(target.ZipPath));

        var info = BocaPackageReader.ReadProblemInfo(problemDir, target.Name);
        var description = BocaPackageReader.ReadDescription(problemDir, info.DescFile);
        var sample = BocaPackageReader.ReadSamplePair(problemDir);
        var limits = BocaPackageReader.CollectLimits(problemDir);
        var testCases = BocaPackageReader.ReadAllTestCases(problemDir);

        var problem = new Problem
        {
            Title = info.FullName,
            Description = description.Html,
            Input = string.Empty,
            Output = string.Empty,
            SampleInput = sample.Input,
            SampleOutput = sample.Output,
            Hint = string.Empty,
            Source = $"BOCA import ({info.BaseName})",
            OriginSource = "BOCA import",
            TimeLimit = limits.TimeLimitSeconds,
            MemoryLimit = limits.MemoryLimitMb,
            Spj = "N",
            Defunct = "N",
            InDate = DateTime.Now,
        };

        var needsReview = description.NeedsReview || sample.NeedsReview;
        if (description.NeedsReview)
        {
            Console.WriteLine($"  ! description needs review: {description.ReviewReason}");
        }
        if (sample.NeedsReview)
        {
            Console.WriteLine($"  ! sample needs review: {sample.ReviewReason}");
        }

        if (options.DryRun)
        {
            Console.WriteLine($"  title: {problem.Title}");
            Console.WriteLine($"  timeLimit={problem.TimeLimit}s memoryLimit={problem.MemoryLimit}MB");
            Console.WriteLine($"  description: {Truncate(description.Html, 200)}");
            Console.WriteLine($"  testCases: {testCases.Count}");
            summary.Add((target.Name, "dry-run", null, needsReview));
            continue;
        }

        var created = await problemService.CreateProblemAsync(options.UserId!, problem, options.SiteId);
        var problemId = created.ProblemId!.Value.ToString();
        for (var i = 0; i < testCases.Count; i++)
        {
            var caseNumber = i + 1;
            fileManager.WriteToFile(problemId, $"{caseNumber}.in", testCases[i].Input);
            fileManager.WriteToFile(problemId, $"{caseNumber}.out", testCases[i].Output);
        }

        if (target.ZipPath is not null)
        {
            Directory.CreateDirectory(ArchiveRoot);
            var archivePath = Path.Combine(ArchiveRoot, $"{problemId}-{Path.GetFileName(target.ZipPath)}");
            File.Copy(target.ZipPath, archivePath, overwrite: true);
        }

        Console.WriteLine($"  created problemId={created.ProblemId} ({testCases.Count} test case(s))");
        summary.Add((target.Name, "created", created.ProblemId?.ToString(), needsReview));
    }
    catch (Exception error)
    {
        Console.WriteLine($"  ERROR: {error.Message}");
        summary.Add((target.Name, "error", error.Message, false));
    }
    finally
    {
        if (tempExtractRoot is not null && Directory.Exists(tempExtractRoot))
        {
            Directory.Delete(tempExtractRoot, recursive: true);
        }
    }
}

Console.WriteLine("\n=== Summary ===");
foreach (var row in summary)
{
    var reviewTag = row.NeedsReview ? " [NEEDS REVIEW]" : "";
    Console.WriteLine(row.Status switch
    {
        "error" => $"- {row.Name}: ERROR - {row.Detail}",
        "dry-run" => $"- {row.Name}: dry-run ok{reviewTag}",
        _ => $"- {row.Name}: created problemId={row.Detail}{reviewTag}",
    });
}

return 0;

static string Truncate(string value, int maxLength) =>
    value.Length <= maxLength ? value : value[..maxLength] + "...";

static string ExtractZip(string zipPath)
{
    var tempDir = Path.Combine(Path.GetTempPath(), "boca-cli-import", Guid.NewGuid().ToString("N"));
    ZipFile.ExtractToDirectory(zipPath, tempDir);
    return tempDir;
}

static List<ImportTarget> ResolveTargets(CommandLineOptions options)
{
    if (options.Targets.Count > 0)
    {
        return options.Targets.Select(name => ResolveOneTarget(options.BocaDir, name)).ToList();
    }

    // No explicit targets: import every .zip under BocaDir, plus any already-extracted
    // folder (besides the reference template) for backwards compatibility.
    var zips = Directory.GetFiles(options.BocaDir, "*.zip")
        .Select(path => new ImportTarget(Path.GetFileNameWithoutExtension(path), path, path));

    var folders = Directory.GetDirectories(options.BocaDir)
        .Where(path => Path.GetFileName(path) != TemplateFolderName)
        .Select(path => new ImportTarget(Path.GetFileName(path)!, path, null));

    return zips.Concat(folders).ToList();
}

static ImportTarget ResolveOneTarget(string bocaDir, string name)
{
    var path = Path.IsPathRooted(name) ? name : Path.Combine(bocaDir, name);

    if (Directory.Exists(path))
    {
        return new ImportTarget(name, path, null);
    }

    var zipPath = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? path : $"{path}.zip";
    if (File.Exists(zipPath))
    {
        return new ImportTarget(Path.GetFileNameWithoutExtension(zipPath), zipPath, zipPath);
    }

    throw new ArgumentException($"Could not find a folder or .zip package named '{name}' under {bocaDir}");
}

sealed record ImportTarget(string Name, string Path, string? ZipPath);
