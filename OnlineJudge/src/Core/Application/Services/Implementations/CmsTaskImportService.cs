using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Statement sections transcribed from the task's PDF.
public sealed record CmsStatement(string Description, string Input, string Output, string Hint);

// Imports a CMS task in the Italian YAML layout as a new problem:
//   task.yaml                      title, time_limit (s), memory_limit (MB), n_input
//   input/input<i>.txt             test inputs, output/output<i>.txt the answers
//   gen/GEN                        "# ST: <points>" opens a subtask; every other line is one test
//   check/checker.cpp              optional CMS checker (cor/correttore.cpp is also accepted)
//   statement/statement.pdf        optional statement (testo/testo.pdf is also accepted)
// Only batch tasks on standard input and output are supported.
public sealed class CmsTaskImportService(IProblemService problems, IFileSystemLocalManagerManager files)
{
    private static readonly Regex SubtaskLine = new(@"^#\s*ST:\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<Problem> ImportAsync(string userId, Stream zipStream, int siteId, Func<string, Task<CmsStatement?>>? readStatement = null)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), "cms-import", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(stagingDir);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(stagingDir);
            }

            var taskFile = Directory.GetFiles(stagingDir, "task.yaml", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault()
                ?? throw new InvalidDataException("El paquete no contiene task.yaml.");
            var root = Path.GetDirectoryName(taskFile)!;
            var task = ReadYaml(taskFile);

            if (task.GetValueOrDefault("output_only") is "true" or "True"
                || !string.IsNullOrWhiteSpace(task.GetValueOrDefault("infile"))
                || !string.IsNullOrWhiteSpace(task.GetValueOrDefault("outfile")))
            {
                throw new InvalidDataException("Solo se admiten tareas que leen de la entrada estándar y escriben en la salida estándar.");
            }

            var tests = ReadTests(root, task);
            var groups = ReadGroups(Path.Combine(root, "gen", "GEN"), tests.Count);
            var checker = new[] { Path.Combine(root, "check", "checker.cpp"), Path.Combine(root, "cor", "correttore.cpp") }
                .FirstOrDefault(File.Exists);
            if (checker == null && (File.Exists(Path.Combine(root, "check", "checker")) || File.Exists(Path.Combine(root, "cor", "correttore"))))
            {
                throw new InvalidDataException("El checker solo está como binario; incluye su código fuente como check/checker.cpp.");
            }

            var pdf = new[] { Path.Combine(root, "statement", "statement.pdf"), Path.Combine(root, "testo", "testo.pdf") }
                .FirstOrDefault(File.Exists);
            var statement = pdf != null && readStatement != null ? await readStatement(pdf) : null;

            var created = await problems.CreateProblemAsync(userId, new Problem
            {
                Title = task.GetValueOrDefault("title") is { Length: > 0 } title ? title : task.GetValueOrDefault("name") ?? "Imported task",
                Description = statement?.Description ?? "<p>Enunciado pendiente: la tarea se importó sin texto.</p>",
                Input = statement?.Input ?? string.Empty,
                Output = statement?.Output ?? string.Empty,
                Hint = statement?.Hint ?? string.Empty,
                Source = "CMS import",
                OriginSource = "CMS import",
                TimeLimit = Math.Max(1, (int)Math.Ceiling(ReadNumber(task, "time_limit", 1))),
                MemoryLimit = Math.Max(1, (int)Math.Ceiling(ReadNumber(task, "memory_limit", 256))),
                Defunct = "N",
                Spj = checker != null ? "Y" : "N",
                InDate = DateTime.Now,
                SampleCases = [],
                Classifications = [],
            }, siteId);

            var folder = created.ProblemId!.Value.ToString();
            files.CreateFolder(folder);
            foreach (var (test, index) in tests.Select((test, index) => (test, index)))
            {
                var name = TestName(groups, index);
                files.WriteToFile(folder, name + ".in", File.ReadAllText(test.Input));
                files.WriteToFile(folder, name + ".out", File.ReadAllText(test.Output));
            }

            files.WriteToFile(folder, ProblemScoringService.FileName, JsonSerializer.Serialize(new
            {
                groups = groups.Select((group, index) => new ProblemScoreGroup
                {
                    Name = groups.Count == 1 ? "Todos los casos" : $"Subtarea {index + 1}",
                    Points = group.Points,
                    Type = group.Type,
                    Tests = [$"s{index + 1:D2}_*"],
                }),
            }, JsonOptions));

            if (checker != null)
            {
                files.WriteToFile(folder, "checker_cms.cpp", File.ReadAllText(checker));
            }

            return created;
        }
        finally
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }
        }
    }

    // ponytail: top-level "key: value" lines only, which is all a task.yaml uses for these fields.
    private static Dictionary<string, string> ReadYaml(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0 || char.IsWhiteSpace(line[0]) || line[0] == '#')
            {
                continue;
            }

            values[line[..colon].Trim()] = line[(colon + 1)..].Trim().Trim('"', '\'');
        }

        return values;
    }

    private static double ReadNumber(Dictionary<string, string> task, string key, double fallback) =>
        double.TryParse(task.GetValueOrDefault(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;

    private static List<(string Input, string Output)> ReadTests(string root, Dictionary<string, string> task)
    {
        var tests = new List<(string, string)>();
        for (var index = 0; ; index++)
        {
            var input = Path.Combine(root, "input", $"input{index}.txt");
            var output = Path.Combine(root, "output", $"output{index}.txt");
            if (!File.Exists(input) || !File.Exists(output))
            {
                break;
            }

            tests.Add((input, output));
        }

        if (tests.Count == 0)
        {
            throw new InvalidDataException("El paquete no contiene casos en input/input0.txt y output/output0.txt.");
        }

        if (int.TryParse(task.GetValueOrDefault("n_input"), out var declared) && declared > 0 && declared != tests.Count)
        {
            throw new InvalidDataException($"task.yaml declara {declared} casos y el paquete trae {tests.Count}.");
        }

        return tests;
    }

    // Without subtasks CMS gives every test the same weight, which is one proportional group of 100.
    // With subtasks it uses GroupMin: a subtask only scores when all its tests pass.
    private static List<(decimal Points, string Type, int Tests)> ReadGroups(string genPath, int testCount)
    {
        var groups = new List<(decimal Points, string Type, int Tests)>();
        if (File.Exists(genPath))
        {
            foreach (var raw in File.ReadAllLines(genPath))
            {
                var line = raw.Trim();
                var subtask = SubtaskLine.Match(line);
                if (subtask.Success)
                {
                    groups.Add((decimal.Parse(subtask.Groups[1].Value, CultureInfo.InvariantCulture), "min", 0));
                }
                else if (groups.Count > 0 && line.Length > 0 && (!line.StartsWith('#') || line.StartsWith("#COPY:", StringComparison.Ordinal)))
                {
                    groups[^1] = groups[^1] with { Tests = groups[^1].Tests + 1 };
                }
            }
        }

        if (groups.Count == 0)
        {
            return [(100m, "sum", testCount)];
        }

        if (groups.Any(group => group.Tests == 0) || groups.Sum(group => group.Tests) != testCount)
        {
            throw new InvalidDataException("Las subtareas de gen/GEN no coinciden con los casos de input/.");
        }

        return groups;
    }

    // s<subtask>_<test>, so each group is the glob s<subtask>_*.
    private static string TestName(List<(decimal Points, string Type, int Tests)> groups, int index)
    {
        var subtask = 0;
        var first = 0;
        while (index >= first + groups[subtask].Tests)
        {
            first += groups[subtask].Tests;
            subtask++;
        }

        return $"s{subtask + 1:D2}_{index - first + 1:D3}";
    }
}
