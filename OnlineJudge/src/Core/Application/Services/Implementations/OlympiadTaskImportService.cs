using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Statement sections transcribed from the task's PDF.
public sealed record TaskStatement(string Description, string Input, string Output, string Hint);

// Imports an olympiad task as a new problem. The format is detected from the zip:
//
// CMS, Italian YAML layout (task.yaml)
//   task.yaml                      title, time_limit (s), memory_limit (MB), n_input, public_testcases
//   input/input<i>.txt             test inputs, output/output<i>.txt the answers
//   gen/GEN                        "# ST: <points>" opens a subtask; every other line is one test
//   check/checker.cpp              optional checker (cor/correttore.cpp is also accepted)
//   sol/grader.cpp, sol/*.h        optional grader and its public headers
//   statement/statement.pdf        optional statement (testo/testo.pdf is also accepted)
//
// TPS, the IOI task preparation system (problem.json), after running "tps gen"
//   problem.json                   title, time_limit (s), memory_limit (MB), type, has_grader, has_checker
//   subtasks.json                  score of each subtask; score 0 marks the samples
//   tests/<name>.in, <name>.out    generated tests, tests/mapping lists "<subtask> <test>"
//   checker/checker.cpp            checker, with its own CMS-compatible checker/testlib.h
//   grader/cpp/grader.cpp, *.h     grader and its public headers, unless has_grader is false
//   statement/                     index.md or a PDF
//
// Only batch tasks are supported. A task with a grader is solved by writing a function and only
// accepts C++: the judge compiles the submission together with grader.cpp.
public sealed class OlympiadTaskImportService(IProblemService problems, IFileSystemLocalManagerManager files)
{
    private const int MaxSamples = 5;
    private const int MaxSampleBytes = 20_000;

    private static readonly Regex SubtaskLine = new(@"^#\s*ST:\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private sealed record TaskTest(string Name, string Input, string Output);

    private sealed record TaskGroup(string Name, decimal Points, string Type, List<string> Tests);

    private sealed class ImportedTask
    {
        public string Title = "Imported task";
        public string Source = string.Empty;
        public double TimeLimit = 1;
        public double MemoryLimit = 256;
        public List<TaskTest> Tests = [];
        public List<TaskGroup> Groups = [];
        public List<TaskTest> Samples = [];
        public string? Checker;
        // Compiled next to the checker: TPS ships a testlib.h of its own.
        public string? CheckerHeader;
        // grader.cpp first, then the headers submissions include.
        public List<string> Grader = [];
        public string? StatementPdf;
        public string? StatementText;
    }

    public async Task<Problem> ImportAsync(string userId, Stream zipStream, int siteId, Func<string, Task<TaskStatement?>>? readStatement = null)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), "task-import", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(stagingDir);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(stagingDir);
            }

            var task = Find(stagingDir, "task.yaml") is { } cms ? ReadCmsTask(cms)
                : Find(stagingDir, "problem.json") is { } tps ? ReadTpsTask(tps)
                : throw new InvalidDataException("El paquete no contiene task.yaml (CMS) ni problem.json (TPS).");

            var statement = task.StatementPdf != null && readStatement != null ? await readStatement(task.StatementPdf) : null;
            var description = statement?.Description
                ?? (task.StatementText != null
                    ? $"<pre style=\"white-space: pre-wrap\">{System.Net.WebUtility.HtmlEncode(task.StatementText)}</pre>"
                    : "<p>Enunciado pendiente: la tarea se importó sin texto.</p>");

            var created = await problems.CreateProblemAsync(userId, new Problem
            {
                Title = task.Title,
                Description = description,
                Input = statement?.Input ?? string.Empty,
                Output = statement?.Output ?? string.Empty,
                Hint = statement?.Hint ?? string.Empty,
                Source = task.Source,
                OriginSource = task.Source,
                TimeLimit = Math.Max(1, (int)Math.Ceiling(task.TimeLimit)),
                MemoryLimit = Math.Max(1, (int)Math.Ceiling(task.MemoryLimit)),
                Defunct = "N",
                Spj = task.Checker != null ? "Y" : "N",
                InDate = DateTime.Now,
                SampleCases = task.Samples
                    .Select((sample, index) => new ProblemSample { Num = index + 1, Input = File.ReadAllText(sample.Input), Output = File.ReadAllText(sample.Output) })
                    .ToList(),
                Classifications = [],
            }, siteId);

            var folder = created.ProblemId!.Value.ToString();
            files.CreateFolder(folder);
            foreach (var test in task.Tests)
            {
                files.WriteToFile(folder, test.Name + ".in", File.ReadAllText(test.Input));
                files.WriteToFile(folder, test.Name + ".out", File.ReadAllText(test.Output));
            }

            files.WriteToFile(folder, ProblemScoringService.FileName, JsonSerializer.Serialize(new
            {
                groups = task.Groups.Select(group => new ProblemScoreGroup { Name = group.Name, Points = group.Points, Type = group.Type, Tests = group.Tests }),
            }, JsonOptions));

            foreach (var (path, index) in task.Grader.Select((path, index) => (path, index)))
            {
                files.WriteToFile(folder, index == 0 ? "grader.cpp" : Path.GetFileName(path), File.ReadAllText(path));
            }

            if (task.Checker != null)
            {
                files.WriteToFile(folder, "checker_cms.cpp", File.ReadAllText(task.Checker));
                if (task.CheckerHeader != null)
                {
                    files.WriteToFile(folder, "testlib.h", File.ReadAllText(task.CheckerHeader));
                }
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

    // The task may be zipped with or without its own top folder.
    private static string? Find(string directory, string fileName) =>
        Directory.GetFiles(directory, fileName, SearchOption.AllDirectories).OrderBy(path => path.Length).FirstOrDefault();

    private static ImportedTask ReadCmsTask(string taskFile)
    {
        var root = Path.GetDirectoryName(taskFile)!;
        var yaml = ReadYaml(taskFile);
        if (yaml.GetValueOrDefault("output_only") is "true" or "True"
            || !string.IsNullOrWhiteSpace(yaml.GetValueOrDefault("infile"))
            || !string.IsNullOrWhiteSpace(yaml.GetValueOrDefault("outfile")))
        {
            throw new InvalidDataException("Solo se admiten tareas que leen de la entrada estándar y escriben en la salida estándar.");
        }

        var task = new ImportedTask
        {
            Title = yaml.GetValueOrDefault("title") is { Length: > 0 } title ? title : yaml.GetValueOrDefault("name") ?? "Imported task",
            Source = "CMS import",
            TimeLimit = ReadNumber(yaml.GetValueOrDefault("time_limit"), 1),
            MemoryLimit = ReadNumber(yaml.GetValueOrDefault("memory_limit"), 256),
            Checker = FirstExisting(Path.Combine(root, "check", "checker.cpp"), Path.Combine(root, "cor", "correttore.cpp")),
            StatementPdf = FirstExisting(Path.Combine(root, "statement", "statement.pdf"), Path.Combine(root, "testo", "testo.pdf")),
        };
        task.Grader = GraderFiles(Path.Combine(root, "sol"), "grader.cpp", required: false);
        if (task.Checker == null && FirstExisting(Path.Combine(root, "check", "checker"), Path.Combine(root, "cor", "correttore")) != null)
        {
            throw new InvalidDataException("El checker solo está como binario; incluye su código fuente como check/checker.cpp.");
        }

        var inputs = new List<(string Input, string Output)>();
        for (var index = 0; File.Exists(Path.Combine(root, "input", $"input{index}.txt")) && File.Exists(Path.Combine(root, "output", $"output{index}.txt")); index++)
        {
            inputs.Add((Path.Combine(root, "input", $"input{index}.txt"), Path.Combine(root, "output", $"output{index}.txt")));
        }

        if (inputs.Count == 0)
        {
            throw new InvalidDataException("El paquete no contiene casos en input/input0.txt y output/output0.txt.");
        }

        if (int.TryParse(yaml.GetValueOrDefault("n_input"), out var declared) && declared > 0 && declared != inputs.Count)
        {
            throw new InvalidDataException($"task.yaml declara {declared} casos y el paquete trae {inputs.Count}.");
        }

        // Without subtasks CMS gives every test the same weight, which is one proportional group of 100.
        // With subtasks it uses GroupMin: a subtask only scores when all its tests pass.
        var sizes = ReadCmsSubtasks(Path.Combine(root, "gen", "GEN"));
        var hasSubtasks = sizes.Count > 0;
        if (!hasSubtasks)
        {
            sizes.Add((100m, inputs.Count));
        }
        else if (sizes.Any(subtask => subtask.Tests == 0) || sizes.Sum(subtask => subtask.Tests) != inputs.Count)
        {
            throw new InvalidDataException("Las subtareas de gen/GEN no coinciden con los casos de input/.");
        }

        var next = 0;
        foreach (var (subtask, index) in sizes.Select((subtask, index) => (subtask, index)))
        {
            var names = new List<string>();
            for (var position = 1; position <= subtask.Tests; position++, next++)
            {
                names.Add($"s{index + 1:D2}_{position:D3}");
                task.Tests.Add(new TaskTest(names[^1], inputs[next].Input, inputs[next].Output));
            }

            task.Groups.Add(new TaskGroup(
                hasSubtasks ? $"Subtarea {index + 1}" : "Todos los casos",
                subtask.Points,
                hasSubtasks ? "min" : "sum",
                [$"s{index + 1:D2}_*"]));
        }

        // public_testcases lists the tests contestants may see: those are the samples.
        task.Samples = (yaml.GetValueOrDefault("public_testcases") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var index) && index >= 0 && index < task.Tests.Count ? task.Tests[index] : null)
            .OfType<TaskTest>()
            .Where(IsSmall)
            .Take(MaxSamples)
            .ToList();
        return task;
    }

    private static ImportedTask ReadTpsTask(string problemFile)
    {
        var root = Path.GetDirectoryName(problemFile)!;
        using var problem = JsonDocument.Parse(File.ReadAllText(problemFile));
        var type = Text(problem.RootElement, "type") ?? "Batch";
        if (type != "Batch")
        {
            throw new InvalidDataException($"Solo se admiten tareas de tipo Batch; esta es {type}.");
        }

        // TPS defaults has_grader to true: the contestant writes a function and the grader owns main().
        var hasGrader = !problem.RootElement.TryGetProperty("has_grader", out var grader) || grader.ValueKind != JsonValueKind.False;

        var testsDir = Path.Combine(root, "tests");
        var mappingFile = Path.Combine(testsDir, "mapping");
        if (!File.Exists(mappingFile))
        {
            throw new InvalidDataException("Falta tests/mapping: ejecuta \"tps gen\" antes de comprimir la tarea.");
        }

        var task = new ImportedTask
        {
            Title = Text(problem.RootElement, "title") is { Length: > 0 } title ? title : Text(problem.RootElement, "name") ?? "Imported task",
            Source = "TPS import",
            TimeLimit = problem.RootElement.TryGetProperty("time_limit", out var time) && time.TryGetDouble(out var seconds) && seconds > 0 ? seconds : 1,
            MemoryLimit = problem.RootElement.TryGetProperty("memory_limit", out var memory) && memory.TryGetDouble(out var megabytes) && megabytes > 0 ? megabytes : 256,
            StatementPdf = Directory.Exists(Path.Combine(root, "statement"))
                ? Directory.GetFiles(Path.Combine(root, "statement"), "*.pdf").OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault()
                : null,
        };
        if (hasGrader)
        {
            task.Grader = GraderFiles(Path.Combine(root, "grader", "cpp"), (Text(problem.RootElement, "grader_name") ?? "grader") + ".cpp", required: true);
        }

        var markdown = Path.Combine(root, "statement", "index.md");
        task.StatementText = File.Exists(markdown) ? File.ReadAllText(markdown) : null;

        var hasChecker = !problem.RootElement.TryGetProperty("has_checker", out var checker) || checker.ValueKind != JsonValueKind.False;
        if (hasChecker)
        {
            task.Checker = FirstExisting(Path.Combine(root, "checker", "checker.cpp"))
                ?? throw new InvalidDataException("La tarea declara checker pero falta checker/checker.cpp.");
            task.CheckerHeader = FirstExisting(Path.Combine(root, "checker", "testlib.h"));
        }

        // tests/mapping: "<subtask> <test>" per line; a test may be listed under several subtasks.
        var testsBySubtask = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(mappingFile))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (!Regex.IsMatch(parts[1], @"^[A-Za-z0-9_.-]+$"))
            {
                throw new InvalidDataException($"Nombre de caso no admitido en tests/mapping: {parts[1]}");
            }

            if (!testsBySubtask.TryGetValue(parts[0], out var list))
            {
                testsBySubtask[parts[0]] = list = [];
            }

            list.Add(parts[1]);
        }

        foreach (var name in testsBySubtask.Values.SelectMany(list => list).Distinct().OrderBy(name => name, StringComparer.Ordinal))
        {
            var input = Path.Combine(testsDir, name + ".in");
            var output = Path.Combine(testsDir, name + ".out");
            if (!File.Exists(input) || !File.Exists(output))
            {
                throw new InvalidDataException($"Falta tests/{name}.in o tests/{name}.out: ejecuta \"tps gen\" antes de comprimir la tarea.");
            }

            task.Tests.Add(new TaskTest(name, input, output));
        }

        var subtasksFile = Path.Combine(root, "subtasks.json");
        if (!File.Exists(subtasksFile))
        {
            throw new InvalidDataException("Falta subtasks.json.");
        }

        using var subtasks = JsonDocument.Parse(File.ReadAllText(subtasksFile));
        var declared = subtasks.RootElement.TryGetProperty("subtasks", out var items) && items.ValueKind == JsonValueKind.Object
            ? items.EnumerateObject()
                .Select(item => (
                    Name: item.Name,
                    Index: item.Value.TryGetProperty("index", out var index) && index.TryGetInt32(out var position) ? position : 0,
                    Score: item.Value.TryGetProperty("score", out var score) && score.TryGetDecimal(out var points) ? points : 0m))
                .OrderBy(item => item.Index)
                .ToList()
            : [];
        foreach (var subtask in declared)
        {
            var names = testsBySubtask.GetValueOrDefault(subtask.Name) ?? [];
            if (subtask.Score <= 0)
            {
                // The zero-score subtask holds the examples of the statement.
                task.Samples.AddRange(names.Select(name => task.Tests.First(test => test.Name == name)).Where(IsSmall).Take(MaxSamples - task.Samples.Count));
                continue;
            }

            if (names.Count == 0)
            {
                throw new InvalidDataException($"La subtarea {subtask.Name} no tiene casos en tests/mapping.");
            }

            // IOI subtasks are all or nothing.
            task.Groups.Add(new TaskGroup(subtask.Name, subtask.Score, "min", names.Distinct().ToList()));
        }

        if (task.Groups.Count == 0)
        {
            throw new InvalidDataException("subtasks.json no define ninguna subtarea con puntaje.");
        }

        return task;
    }

    // The C++ grader of a task and the headers next to it. Other languages have no grader here.
    private static List<string> GraderFiles(string directory, string graderFile, bool required)
    {
        var grader = Path.Combine(directory, graderFile);
        if (!File.Exists(grader))
        {
            return required
                ? throw new InvalidDataException($"La tarea usa grader pero falta {Path.GetFileName(directory)}/{graderFile}. Solo se admiten graders de C++.")
                : [];
        }

        return [grader, .. Directory.GetFiles(directory, "*.h").OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static List<(decimal Points, int Tests)> ReadCmsSubtasks(string genPath)
    {
        var subtasks = new List<(decimal Points, int Tests)>();
        if (!File.Exists(genPath))
        {
            return subtasks;
        }

        foreach (var raw in File.ReadAllLines(genPath))
        {
            var line = raw.Trim();
            var subtask = SubtaskLine.Match(line);
            if (subtask.Success)
            {
                subtasks.Add((decimal.Parse(subtask.Groups[1].Value, CultureInfo.InvariantCulture), 0));
            }
            else if (subtasks.Count > 0 && line.Length > 0 && (!line.StartsWith('#') || line.StartsWith("#COPY:", StringComparison.Ordinal)))
            {
                subtasks[^1] = (subtasks[^1].Points, subtasks[^1].Tests + 1);
            }
        }

        return subtasks;
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

    private static double ReadNumber(string? text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists);

    // A sample is shown in the statement: skip the tests that are too large to read there.
    private static bool IsSmall(TaskTest test) =>
        new FileInfo(test.Input).Length <= MaxSampleBytes && new FileInfo(test.Output).Length <= MaxSampleBytes;
}
