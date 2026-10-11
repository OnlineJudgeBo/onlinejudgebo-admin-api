using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

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
// TPS exported for CMS with "tps export" (problem.json next to a subtasks/ folder)
//   problem.json                   name (the title), time_limit (s), memory_limit (bytes), task_type
//   subtasks/<nn>-<name>.json      score and testcases of each subtask; score 0 marks the samples
//   tests/<name>.in, <name>.out    the tests
//   checker/checker.cpp, testlib.h optional checker
//   graders/grader.cpp, *.h        grader and headers, when the task is compiled with a grader
//   statement/statement_<code>.tex statement in LaTeX; without it, statement/statement.pdf
//
// Only batch tasks are supported. A task with a grader is solved by writing a function: the judge
// builds the submission with the grader of its language (grader.cpp, grader.java or grader.py).
public sealed class OlympiadTaskImportService(IProblemService problems, IFileSystemLocalManagerManager files, IStatementPdfReader statements) : IOlympiadTaskImportService
{
    private const int MaxSamples = 5;
    private const int MaxSampleBytes = 20_000;

    private static readonly Regex SubtaskLine = new(@"^#\s*ST:\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private const string Braces = @"\{(?>[^{}]+|\{(?<open>)|\}(?<-open>))*(?(open)(?!))\}";

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
        // The graders and the headers C++ submissions include, by the name the judge expects.
        public List<(string Name, string Path)> Grader = [];
        public TaskStatement? Statement;
        public string? StatementPdf;
        public string? StatementText;
    }

    public async Task<Problem> ImportAsync(string userId, Stream zipStream, int siteId)
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

            var statement = task.Statement ?? (task.StatementPdf != null ? await statements.ReadAsync(task.StatementPdf) : null);
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

            files.WriteToFile(folder, ProblemScoringRules.FileName, ProblemScoringRules.Serialize(
                task.Groups.Select(group => new ProblemScoreGroup { Name = group.Name, Points = group.Points, Type = group.Type, Tests = group.Tests })));

            foreach (var (name, path) in task.Grader)
            {
                files.WriteToFile(folder, name, File.ReadAllText(path));
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
        task.Grader = GraderFiles(Path.Combine(root, "sol"), Path.Combine(root, "sol"), "grader", required: false);
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
        // "tps export" writes the layout CMS loads; without it this is the task repository itself.
        var exported = Directory.Exists(Path.Combine(root, "subtasks"));
        var type = Text(problem.RootElement, "type") ?? Text(problem.RootElement, "task_type") ?? "Batch";
        if (type != "Batch")
        {
            throw new InvalidDataException($"Solo se admiten tareas de tipo Batch; esta es {type}.");
        }

        // With a grader the contestant writes a function and the grader owns main(). The repository
        // defaults has_grader to true; the export says how submissions are compiled.
        var hasGrader = exported
            ? (Text(problem.RootElement, "task_type_params") ?? string.Empty).Contains("\"grader\"", StringComparison.Ordinal)
            : !problem.RootElement.TryGetProperty("has_grader", out var grader) || grader.ValueKind != JsonValueKind.False;

        var testsDir = Path.Combine(root, "tests");
        var mappingFile = Path.Combine(testsDir, "mapping");
        if (!exported && !File.Exists(mappingFile))
        {
            throw new InvalidDataException("Falta tests/mapping: ejecuta \"tps gen\" antes de comprimir la tarea.");
        }

        var memory = problem.RootElement.TryGetProperty("memory_limit", out var limit) && limit.TryGetDouble(out var amount) && amount > 0 ? amount : 256;
        var task = new ImportedTask
        {
            Title = Text(problem.RootElement, "title") is { Length: > 0 } title ? title : Text(problem.RootElement, "name") ?? "Imported task",
            Source = "TPS import",
            TimeLimit = problem.RootElement.TryGetProperty("time_limit", out var time) && time.TryGetDouble(out var seconds) && seconds > 0 ? seconds : 1,
            // The export gives the limit in bytes, the repository in megabytes.
            MemoryLimit = exported ? memory / (1024 * 1024) : memory,
            StatementPdf = Directory.Exists(Path.Combine(root, "statement"))
                ? Directory.GetFiles(Path.Combine(root, "statement"), "*.pdf").OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault()
                : null,
        };
        if (hasGrader)
        {
            task.Grader = exported
                ? GraderFiles(Path.Combine(root, "graders"), Path.Combine(root, "graders"), "grader", required: true)
                : GraderFiles(Path.Combine(root, "grader", "cpp"), Path.Combine(root, "grader"), Text(problem.RootElement, "grader_name") ?? "grader", required: true);
        }

        // The export brings the source of the statement, which keeps the formulas the PDF text loses.
        var tex = exported && Directory.Exists(Path.Combine(root, "statement"))
            ? Directory.GetFiles(Path.Combine(root, "statement"), "statement_*.tex").OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault(path => !path.EndsWith("_en.tex"))
            : null;
        task.Statement = tex != null ? ReadTexStatement(File.ReadAllText(tex)) : null;

        var markdown = Path.Combine(root, "statement", "index.md");
        task.StatementText = File.Exists(markdown) ? File.ReadAllText(markdown) : null;

        // The repository says whether there is a checker; in the export it is there or it is not.
        task.Checker = FirstExisting(Path.Combine(root, "checker", "checker.cpp"));
        var hasChecker = exported
            ? task.Checker != null
            : !problem.RootElement.TryGetProperty("has_checker", out var checker) || checker.ValueKind != JsonValueKind.False;
        if (hasChecker && task.Checker == null)
        {
            throw new InvalidDataException("La tarea declara checker pero falta checker/checker.cpp.");
        }

        task.Checker = hasChecker ? task.Checker : null;
        task.CheckerHeader = hasChecker ? FirstExisting(Path.Combine(root, "checker", "testlib.h")) : null;

        // Subtasks in order, with their score, and the tests of each; a test may be in several.
        var (declared, testsBySubtask) = exported ? ReadExportedSubtasks(Path.Combine(root, "subtasks")) : ReadSubtasks(root, mappingFile);
        foreach (var name in testsBySubtask.Values.SelectMany(list => list).Distinct())
        {
            if (!Regex.IsMatch(name, @"^[A-Za-z0-9_.-]+$"))
            {
                throw new InvalidDataException($"Nombre de caso no admitido: {name}");
            }
        }

        foreach (var name in testsBySubtask.Values.SelectMany(list => list).Distinct().OrderBy(name => name, StringComparer.Ordinal))
        {
            var input = Path.Combine(testsDir, name + ".in");
            var output = Path.Combine(testsDir, name + ".out");
            if (!File.Exists(input) || !File.Exists(output))
            {
                throw new InvalidDataException($"Falta tests/{name}.in o tests/{name}.out.");
            }

            task.Tests.Add(new TaskTest(name, input, output));
        }

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
                throw new InvalidDataException($"La subtarea {subtask.Name} no tiene casos.");
            }

            // IOI subtasks are all or nothing.
            task.Groups.Add(new TaskGroup(subtask.Name, subtask.Score, "min", names.Distinct().ToList()));
        }

        if (task.Groups.Count == 0)
        {
            throw new InvalidDataException("La tarea no define ninguna subtarea con puntaje.");
        }

        return task;
    }

    // Task repository: subtasks.json has the scores and tests/mapping lists "<subtask> <test>" per line.
    private static (List<(string Name, decimal Score)>, Dictionary<string, List<string>>) ReadSubtasks(string root, string mappingFile)
    {
        var tests = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(mappingFile))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (!tests.TryGetValue(parts[0], out var list))
            {
                tests[parts[0]] = list = [];
            }

            list.Add(parts[1]);
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
                .Select(item => (item.Name, item.Score))
                .ToList()
            : [];
        return (declared, tests);
    }

    // Export: one file per subtask, subtasks/<nn>-<name>.json, with its score and its tests.
    private static (List<(string Name, decimal Score)>, Dictionary<string, List<string>>) ReadExportedSubtasks(string directory)
    {
        var declared = new List<(string Name, decimal Score)>();
        var tests = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            using var subtask = JsonDocument.Parse(File.ReadAllText(file));
            var name = Regex.Replace(Path.GetFileNameWithoutExtension(file), @"^\d+-", string.Empty);
            declared.Add((name, subtask.RootElement.TryGetProperty("score", out var score) && score.TryGetDecimal(out var points) ? points : 0m));
            tests[name] = subtask.RootElement.TryGetProperty("testcases", out var names) && names.ValueKind == JsonValueKind.Array
                ? names.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList()
                : [];
        }

        return (declared, tests);
    }

    // The C++ grader of a task with the headers next to it, and the Java and Python graders when
    // the task brings them. `others` is where those live: the same folder, or one folder per language.
    private static List<(string Name, string Path)> GraderFiles(string directory, string others, string graderName, bool required)
    {
        var grader = Path.Combine(directory, graderName + ".cpp");
        if (!File.Exists(grader))
        {
            return required
                ? throw new InvalidDataException($"La tarea usa grader pero falta {Path.GetFileName(directory)}/{graderName}.cpp.")
                : [];
        }

        var found = new List<(string Name, string Path)> { ("grader.cpp", grader) };
        found.AddRange(Directory.GetFiles(directory, "*.h").OrderBy(path => path, StringComparer.Ordinal).Select(path => (Path.GetFileName(path), path)));
        foreach (var (extension, folder) in new[] { ("java", "java"), ("py", "py") })
        {
            if (FirstExisting(Path.Combine(others, "grader." + extension), Path.Combine(others, folder, "grader." + extension)) is { } path)
            {
                found.Add(("grader." + extension, path));
            }
        }

        return found;
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
    // Statement of the TPS template as HTML. The formulas stay in LaTeX: the site shows them with MathJax.
    // Images and drawings are not imported, a note is left in their place; the sample tables are dropped
    // because the samples come from the tests.
    private static TaskStatement ReadTexStatement(string tex)
    {
        tex = Regex.Replace(tex.Replace("\r", string.Empty), @"(?<!\\)%.*", string.Empty);
        tex = tex.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        var formulas = new List<string>();
        tex = Regex.Replace(tex, @"(?<!\\)(\$\$.+?\$\$|\$.+?\$|\\\[.+?\\\])", match =>
        {
            formulas.Add(match.Value);
            return $"\u0001{formulas.Count - 1}\u0001";
        }, RegexOptions.Singleline);

        const string figure = "\n\n<p><em>[Figura: agrégala desde el editor]</em></p>\n\n";
        foreach (Match macro in Regex.Matches(tex, @"\\newcommand\{\\(\w+)\}"))
        {
            tex = Regex.Replace(tex, $@"\\{macro.Groups[1].Value}(?:\s*{Braces})+", figure);
        }

        tex = Regex.Replace(tex, $@"\\newcommand\{{\\\w+\}}(?:\[\d\])?\s*{Braces}|\\def\\\w+{Braces}|\\problem{Braces}{Braces}", string.Empty);
        tex = Regex.Replace(tex, @"\\begin\{tabular\}.*?\\end\{tabular\}", string.Empty, RegexOptions.Singleline);
        tex = Regex.Replace(tex, @"\\begin\{tikzpicture\}.*?\\end\{tikzpicture\}", figure, RegexOptions.Singleline);
        tex = Regex.Replace(tex, @"\\includegraphics(?:\[[^\]]*\])?\{[^}]*?([^/}]+)\}", "\n\n<p><em>[Imagen $1: agrégala desde el editor]</em></p>\n\n");
        tex = Regex.Replace(tex, @"\\(?:begin|end)\{center\}", string.Empty);
        tex = Regex.Replace(tex, @"\\begin\{(itemize|enumerate)\}", match => match.Groups[1].Value == "itemize" ? "\n\n<ul>" : "\n\n<ol>");
        tex = Regex.Replace(tex, @"\\end\{(itemize|enumerate)\}", match => match.Groups[1].Value == "itemize" ? "</ul>\n\n" : "</ol>\n\n");
        tex = Regex.Replace(tex, @"\s*\\item\b\s*", "\n<li>");

        tex = Regex.Replace(tex, @"\{\\ttfamily\\char'15\}", "'");
        foreach (var (command, tag) in new[] { ("textbf|bf|bfseries", "b"), ("textit|emph|it", "i"), ("texttt|tt|ttfamily", "code"), ("underline", "u") })
        {
            // Twice, for a command inside another.
            for (var pass = 0; pass < 2; pass++)
            {
                tex = Regex.Replace(tex, $@"\\(?:{command})\{{([^{{}}]*)\}}|\{{\\(?:{command})\b\s*([^{{}}]*)\}}", $"<{tag}>$1$2</{tag}>");
            }
        }

        tex = Regex.Replace(tex, @"\\([#%_{}])", "$1").Replace(@"\&amp;", "&amp;").Replace(@"\ldots", "…").Replace("``", "“").Replace("''", "”").Replace("~", " ");
        tex = Regex.Replace(tex, @"\\\\(?:\[[^\]]*\])?", "<br>");

        // Entrada and Salida have their own field; the other sections go after them, with their title.
        var parts = Regex.Split(tex, @"\\(?:sub)*section\*?\{([^}]*)\}");
        string input = string.Empty, output = string.Empty, hint = string.Empty;
        for (var index = 1; index + 1 < parts.Length; index += 2)
        {
            var title = parts[index].Trim();
            var body = Paragraphs(parts[index + 1]);
            if (title.Equals("Entrada", StringComparison.OrdinalIgnoreCase))
            {
                input += body;
            }
            else if (title.Equals("Salida", StringComparison.OrdinalIgnoreCase))
            {
                output += body;
            }
            else if (body.Length > 0)
            {
                hint += $"<h3>{title}</h3>\n{body}";
            }
        }

        return new TaskStatement(Paragraphs(parts[0]), input, output, hint);

        // The site shows paragraphs without a margin: an empty line keeps them apart.
        string Paragraphs(string text) => Regex.Replace(
            string.Concat(Regex.Split(text, @"\n\s*\n").Select(block => block.Trim()).Where(block => block.Length > 0)
                .Select(block => block.StartsWith('<') && !Regex.IsMatch(block, "^<(b|i|u|code)>") ? block + "\n" : $"<p>{block}</p>\n")).Replace("</p>\n<", "</p>\n<br>\n<"),
            "\u0001(\\d+)\u0001", match => formulas[int.Parse(match.Groups[1].Value)]);
    }

}
