using System.IO.Compression;
using System.Text;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class OlympiadTaskImportServiceTests
{
    private readonly Mock<IProblemService> _problems = new();
    private readonly Mock<IFileSystemLocalManagerManager> _files = new();
    private readonly Dictionary<string, string> _written = new();
    private Problem? _created;

    public OlympiadTaskImportServiceTests()
    {
        _problems.Setup(item => item.CreateProblemAsync("admin", It.IsAny<Problem>(), 1))
            .ReturnsAsync((string _, Problem problem, int _) => { problem.ProblemId = 77; _created = problem; return problem; });
        _files.Setup(item => item.WriteToFile("77", It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, name, content) => _written[name] = content);
    }

    private Task<Problem> Import(Dictionary<string, string> entries, Func<string, Task<TaskStatement?>>? readStatement = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return new OlympiadTaskImportService(_problems.Object, _files.Object).ImportAsync("admin", stream, 1, readStatement);
    }

    private static Dictionary<string, string> Task(int tests, string gen = "", string yaml = "")
    {
        var entries = new Dictionary<string, string>
        {
            ["suma/task.yaml"] = "name: suma\ntitle: \"Suma de pares\"\ntime_limit: 1.5\nmemory_limit: 64\nn_input: " + tests + "\n" + yaml,
        };
        if (gen.Length > 0)
        {
            entries["suma/gen/GEN"] = gen;
        }

        for (var index = 0; index < tests; index++)
        {
            entries[$"suma/input/input{index}.txt"] = "in" + index;
            entries[$"suma/output/output{index}.txt"] = "out" + index;
        }

        return entries;
    }

    [Fact]
    public async Task Import_TurnsSubtasksIntoAllOrNothingGroups()
    {
        var entries = Task(3, "# comentario\n# ST: 30\n1 2\n#COPY: manual.in\n\n#ST: 70\n9 9\n");
        entries["suma/check/checker.cpp"] = "// cms checker";
        entries["suma/statement/statement.pdf"] = "%PDF-";

        await Import(entries, _ => System.Threading.Tasks.Task.FromResult<TaskStatement?>(new TaskStatement("<p>d</p>", "<p>i</p>", "<p>o</p>", "")));

        Assert.Equal("Suma de pares", _created!.Title);
        Assert.Equal(2, _created.TimeLimit);
        Assert.Equal(64, _created.MemoryLimit);
        Assert.Equal("Y", _created.Spj);
        Assert.Equal("<p>i</p>", _created.Input);
        Assert.Equal("in1", _written["s01_002.in"]);
        Assert.Equal("out2", _written["s02_001.out"]);
        Assert.Equal("// cms checker", _written["checker_cms.cpp"]);
        var scoring = ProblemScoringService.Parse(Encoding.UTF8.GetBytes(_written["scoring.json"]));
        Assert.Equal(new[] { 30m, 70m }, scoring.Groups.Select(group => group.Points));
        Assert.All(scoring.Groups, group => Assert.Equal("min", group.Type));
        Assert.Equal(new[] { "s02_*" }, scoring.Groups[1].Tests);
    }

    private static Dictionary<string, string> Tps(string problemExtra = "\"has_grader\": false", bool withTests = true)
    {
        var entries = new Dictionary<string, string>
        {
            ["problem.json"] = "{\"name\": \"suma\", \"title\": \"Suma TPS\", \"type\": \"Batch\", \"time_limit\": 0.5, \"memory_limit\": 128, " + problemExtra + "}",
            ["subtasks.json"] = "{\"subtasks\": {\"full\": {\"index\": 2, \"score\": 60}, \"samples\": {\"index\": 0, \"score\": 0}, \"easy\": {\"index\": 1, \"score\": 40}}}",
            ["checker/checker.cpp"] = "// tps checker",
            ["checker/testlib.h"] = "// tps testlib",
            ["statement/index.md"] = "# Suma\n\nDados <a> y <b>.",
        };
        if (withTests)
        {
            entries["tests/mapping"] = "samples 0-01\neasy 0-01\neasy 1-01\nfull 0-01\nfull 1-01\nfull 2-01\n";
            foreach (var name in new[] { "0-01", "1-01", "2-01" })
            {
                entries[$"tests/{name}.in"] = "in " + name;
                entries[$"tests/{name}.out"] = "out " + name;
            }
        }

        return entries;
    }

    [Fact]
    public async Task Import_ReadsATpsTaskWithItsSubtasksSamplesAndChecker()
    {
        await Import(Tps());

        Assert.Equal("Suma TPS", _created!.Title);
        Assert.Equal(1, _created.TimeLimit);
        Assert.Equal(128, _created.MemoryLimit);
        Assert.Equal("Y", _created.Spj);
        Assert.Contains("Dados &lt;a&gt; y &lt;b&gt;.", _created.Description);
        var sample = Assert.Single(_created.SampleCases);
        Assert.Equal(("in 0-01", "out 0-01"), (sample.Input, sample.Output));
        Assert.Equal("in 2-01", _written["2-01.in"]);
        Assert.Equal("// tps checker", _written["checker_cms.cpp"]);
        Assert.Equal("// tps testlib", _written["testlib.h"]);
        var scoring = ProblemScoringService.Parse(Encoding.UTF8.GetBytes(_written["scoring.json"]));
        Assert.Equal(new[] { "easy", "full" }, scoring.Groups.Select(group => group.Name));
        Assert.Equal(new[] { 40m, 60m }, scoring.Groups.Select(group => group.Points));
        Assert.Equal(new[] { "0-01", "1-01", "2-01" }, scoring.Groups[1].Tests);
        Assert.All(scoring.Groups, group => Assert.Equal("min", group.Type));
    }

    [Theory]
    [InlineData("\"has_grader\": true", true)]
    [InlineData("\"description\": \"sin has_grader\"", true)]
    [InlineData("\"has_grader\": false, \"type\": \"Communication\"", true)]
    [InlineData("\"has_grader\": false", false)]
    public async Task Import_RejectsTpsTasksItCannotJudge(string problemExtra, bool withTests)
    {
        var entries = Tps(problemExtra, withTests);
        if (problemExtra.Contains("Communication"))
        {
            entries["problem.json"] = entries["problem.json"].Replace("\"type\": \"Batch\", ", string.Empty);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => Import(entries));

        Assert.Null(_created);
    }

    [Fact]
    public async Task Import_UsesThePublicCmsTestsAsSamples()
    {
        await Import(Task(3, yaml: "public_testcases: 0, 2\n"));

        Assert.Equal(new[] { "in0", "in2" }, _created!.SampleCases.Select(sample => sample.Input));
        Assert.Equal(new[] { 1, 2 }, _created.SampleCases.Select(sample => sample.Num));
    }

    [Fact]
    public async Task Import_WithoutSubtasksScoresEveryTestEqually()
    {
        await Import(Task(4));

        Assert.Equal("N", _created!.Spj);
        var group = Assert.Single(ProblemScoringService.Parse(Encoding.UTF8.GetBytes(_written["scoring.json"])).Groups);
        Assert.Equal((100m, "sum"), (group.Points, group.Type));
        Assert.Contains("s01_004.in", _written.Keys);
    }

    [Theory]
    [InlineData("output_only: true\n", "")]
    [InlineData("infile: input.txt\n", "")]
    [InlineData("", "# ST: 50\n1\n# ST: 50\n")]
    [InlineData("", "# ST: 100\n1\n")]
    public async Task Import_RejectsWhatItCannotJudge(string yaml, string gen)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(Task(2, gen, yaml)));

        Assert.Null(_created);
    }

    [Fact]
    public async Task Import_RejectsABinaryOnlyChecker_AndAPackageWithoutTask()
    {
        var binaryChecker = Task(1);
        binaryChecker["suma/check/checker"] = "ELF";

        await Assert.ThrowsAsync<InvalidDataException>(() => Import(binaryChecker));
        await Assert.ThrowsAsync<InvalidDataException>(() => Import(new Dictionary<string, string> { ["readme.txt"] = "x" }));
    }
}
