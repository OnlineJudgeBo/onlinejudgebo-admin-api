using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemPackageServiceTests
{
    [Fact]
    public async Task ExportProblemPackageAsync_ThrowsWhenProblemNotFound()
    {
        var problemService = new Mock<IProblemService>();
        problemService.Setup(item => item.GetProblemByIdAsync(1, 1)).ReturnsAsync((Problem)null!);
        var service = CreateService(problemService.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ExportProblemPackageAsync(1, 1));
    }

    [Fact]
    public async Task ExportProblemPackageAsync_ThrowsWhenSpjIsY_WithoutTouchingTheFilesystem()
    {
        var problemService = new Mock<IProblemService>();
        problemService.Setup(item => item.GetProblemByIdAsync(1, 1)).ReturnsAsync(new Problem { ProblemId = 1, Spj = "Y" });
        var fileManager = new Mock<IFileSystemLocalManagerManager>();
        var service = CreateService(problemService.Object, fileManager.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportProblemPackageAsync(1, 1));

        fileManager.Verify(item => item.ListFiles(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_WritesProblemYamlWithNameLimitsAndUuid()
    {
        var problem = BuildProblem(problemId: 5, title: "Suma de dos números", timeLimit: 2, memoryLimit: 256);
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        var yaml = ReadEntryText(zip, "problem.yaml");
        Assert.Contains("name: \"Suma de dos números\"", yaml);
        Assert.Contains("time_limit: 2", yaml);
        Assert.Contains("memory: 256", yaml);
        Assert.Matches(@"uuid: [0-9a-fA-F-]{36}", yaml);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_FallsBackToOriginSourceWhenSourceIsBlank()
    {
        var problem = BuildProblem(problemId: 5);
        problem.Source = "   ";
        problem.OriginSource = "OBI 2024";
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        var yaml = ReadEntryText(zip, "problem.yaml");
        Assert.Contains("source: \"OBI 2024\"", yaml);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_EscapesQuotesAndNewlinesInYamlName()
    {
        var problem = BuildProblem(problemId: 5, title: "Problema \"raro\"\ncon salto de línea");
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        var yaml = ReadEntryText(zip, "problem.yaml");
        var nameLine = yaml.Split('\n').Single(line => line.StartsWith("name:"));
        Assert.Equal("name: \"Problema \\\"raro\\\" con salto de línea\"", nameLine.TrimEnd('\r'));
    }

    [Fact]
    public async Task ExportProblemPackageAsync_WritesSampleCasesUnderDataSample()
    {
        var problem = BuildProblem(problemId: 5);
        problem.SampleCases = new List<ProblemSample>
        {
            new() { Num = 2, Input = "in-2", Output = "out-2" },
            new() { Num = 1, Input = "in-1", Output = "out-1" },
        };
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        Assert.Equal("in-1", ReadEntryText(zip, "data/sample/1.in"));
        Assert.Equal("out-1", ReadEntryText(zip, "data/sample/1.ans"));
        Assert.Equal("in-2", ReadEntryText(zip, "data/sample/2.in"));
        Assert.Equal("out-2", ReadEntryText(zip, "data/sample/2.ans"));
    }

    [Fact]
    public async Task ExportProblemPackageAsync_CopiesSecretTestData_ExcludesSampleFiles_RenamesOutToAns()
    {
        var problem = BuildProblem(problemId: 5);
        var fileManager = new Mock<IFileSystemLocalManagerManager>();
        fileManager.Setup(item => item.ListFiles("5"))
            .Returns(new List<string> { "1.in", "1.out", "sample.in", "sample.out", "checker-notes.txt" });
        fileManager.Setup(item => item.ReadFile("5", "1.in")).Returns(Encoding.UTF8.GetBytes("secret-in"));
        fileManager.Setup(item => item.ReadFile("5", "1.out")).Returns(Encoding.UTF8.GetBytes("secret-out"));
        fileManager.Setup(item => item.ReadFile("5", "checker-notes.txt")).Returns(Encoding.UTF8.GetBytes("notes"));
        var service = CreateService(StubProblemService(5, problem), fileManager.Object);

        var zip = await service.ExportProblemPackageAsync(5, 1);

        Assert.Equal("secret-in", ReadEntryText(zip, "data/secret/1.in"));
        Assert.Equal("secret-out", ReadEntryText(zip, "data/secret/1.ans"));
        Assert.Equal("notes", ReadEntryText(zip, "data/secret/checker-notes.txt"));
        Assert.Null(FindEntry(zip, "data/secret/sample.in"));
        Assert.Null(FindEntry(zip, "data/secret/sample.out"));
        fileManager.Verify(item => item.ReadFile("5", "sample.in"), Times.Never);
        fileManager.Verify(item => item.ReadFile("5", "sample.out"), Times.Never);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_ProducesMarkdownWithoutHtmlTagsOrEntities()
    {
        var problem = BuildProblem(problemId: 5);
        problem.Description = "<p>Hola <b>mundo</b> &amp; bienvenido</p>";
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        var markdown = ReadEntryText(zip, "statement/es/problem.md");
        Assert.DoesNotContain("<", markdown);
        Assert.DoesNotContain(">", markdown);
        Assert.Contains("Hola mundo & bienvenido", markdown);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_ExtractsBase64ImageAndRewritesHtmlSrc()
    {
        var imageBytes = Encoding.UTF8.GetBytes("fake-png-bytes");
        var base64 = Convert.ToBase64String(imageBytes);
        var problem = BuildProblem(problemId: 5);
        problem.Description = $"<p>ver<img src=\"data:image/png;base64,{base64}\"></p>";
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        var imageEntry = FindEntry(zip, "statement/es/img/1.png");
        Assert.NotNull(imageEntry);
        Assert.Equal(imageBytes, ReadEntryBytes(zip, "statement/es/img/1.png"));

        var html = ReadEntryText(zip, "statement/es/problem.html");
        Assert.Contains("img/1.png", html);
        Assert.DoesNotContain(base64, html);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_DeduplicatesRepeatedIdenticalImageSource()
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("same-image"));
        var src = $"data:image/png;base64,{base64}";
        var problem = BuildProblem(problemId: 5);
        problem.Description = $"<img src=\"{src}\"> texto <img src=\"{src}\">";
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var imageEntries = archive.Entries.Where(entry => entry.FullName.StartsWith("statement/es/img/")).ToList();
        Assert.Single(imageEntries);
    }

    [Fact]
    public async Task ExportProblemPackageAsync_LeavesMalformedDataUriUntouchedWithoutThrowing()
    {
        var problem = BuildProblem(problemId: 5);
        problem.Description = "<img src=\"data:image/png;base64,not-valid-base64!!!\">";
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        var html = ReadEntryText(zip, "statement/es/problem.html");
        Assert.Contains("data:image/png;base64,not-valid-base64!!!", html);
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("statement/es/img/"));
    }

    [Fact]
    public async Task ExportProblemPackageAsync_WritesMetadataJsonWithAllRoundTripFields()
    {
        var problem = BuildProblem(problemId: 5, title: "Suma", timeLimit: 3, memoryLimit: 512);
        problem.Hint = "usa suma directa";
        problem.Source = "OBI";
        problem.OriginSource = "OBI 2024";
        problem.Defunct = "N";
        problem.Classifications = new List<Classification> { new() { ClassificationId = 7 }, new() { ClassificationId = 9 } };
        var service = CreateService(StubProblemService(5, problem));

        var zip = await service.ExportProblemPackageAsync(5, 1);

        using var json = JsonDocument.Parse(ReadEntryText(zip, "metadata.json"));
        var root = json.RootElement;
        Assert.Equal("Suma", root.GetProperty("Title").GetString());
        Assert.Equal("usa suma directa", root.GetProperty("Hint").GetString());
        Assert.Equal(3, root.GetProperty("TimeLimit").GetInt32());
        Assert.Equal(512, root.GetProperty("MemoryLimit").GetInt32());
        var classificationIds = root.GetProperty("ClassificationIds").EnumerateArray().Select(item => item.GetInt32()).ToList();
        Assert.Equal(new[] { 7, 9 }, classificationIds);
    }

    [Fact]
    public async Task ImportProblemPackageAsync_UsesMetadataJsonAsSourceOfTruthAndWritesSecretDataBack()
    {
        var problemService = new Mock<IProblemService>();
        Problem? capturedProblem = null;
        problemService
            .Setup(item => item.CreateProblemAsync("teacher", It.IsAny<Problem>(), 1))
            .Callback<string, Problem, int>((_, problem, _) => capturedProblem = problem)
            .ReturnsAsync((string _, Problem problem, int _) =>
            {
                problem.ProblemId = 42;
                return problem;
            });

        var fileManager = new Mock<IFileSystemLocalManagerManager>();
        var service = CreateService(problemService.Object, fileManager.Object);

        var zipBytes = BuildZip(entries =>
        {
            entries["metadata.json"] = JsonSerializer.Serialize(new
            {
                Title = "Importado",
                Description = "<p>desc</p>",
                Input = "in",
                Output = "out",
                Hint = "pista",
                Source = "Fuente",
                OriginSource = "Origen",
                Defunct = "N",
                TimeLimit = 4,
                MemoryLimit = 256,
                ClassificationIds = new[] { 3 },
            });
            entries["data/sample/1.in"] = "sample-in";
            entries["data/sample/1.ans"] = "sample-out";
            entries["data/secret/1.in"] = "secret-in";
            entries["data/secret/1.ans"] = "secret-out";
        });

        using var stream = new MemoryStream(zipBytes);
        var result = await service.ImportProblemPackageAsync("teacher", stream, 1);

        Assert.Equal(42, result.ProblemId);
        Assert.NotNull(capturedProblem);
        Assert.Equal("Importado", capturedProblem!.Title);
        Assert.Equal("<p>desc</p>", capturedProblem.Description);
        Assert.Equal("in", capturedProblem.Input);
        Assert.Equal("out", capturedProblem.Output);
        Assert.Equal("pista", capturedProblem.Hint);
        Assert.Equal(4, capturedProblem.TimeLimit);
        Assert.Equal(256, capturedProblem.MemoryLimit);
        Assert.Equal("N", capturedProblem.Spj);
        Assert.Single(capturedProblem.Classifications!, c => c.ClassificationId == 3);

        var sample = Assert.Single(capturedProblem.SampleCases);
        Assert.Equal(1, sample.Num);
        Assert.Equal("sample-in", sample.Input);
        Assert.Equal("sample-out", sample.Output);

        fileManager.Verify(item => item.CreateFolder("42"), Times.Once);
        fileManager.Verify(item => item.WriteToFile("42", "1.in", "secret-in"), Times.Once);
        fileManager.Verify(item => item.WriteToFile("42", "1.out", "secret-out"), Times.Once);
    }

    [Fact]
    public async Task ImportProblemPackageAsync_FallsBackToYamlAndMarkdownWhenNoMetadataJson()
    {
        var problemService = new Mock<IProblemService>();
        Problem? captured = null;
        problemService
            .Setup(item => item.CreateProblemAsync(It.IsAny<string>(), It.IsAny<Problem>(), It.IsAny<int>()))
            .Callback<string, Problem, int>((_, problem, _) => captured = problem)
            .ReturnsAsync((string _, Problem problem, int _) => { problem.ProblemId = 7; return problem; });
        var service = CreateService(problemService.Object);

        var zipBytes = BuildZip(entries =>
        {
            entries["problem.yaml"] = "name: \"Paquete externo\"\nsource: \"Codeforces\"\nlimits:\n  time_limit: 5\n  memory: 1024\n";
            entries["statement/english/problem.md"] = "Texto plano del enunciado.";
        });

        using var stream = new MemoryStream(zipBytes);

        await service.ImportProblemPackageAsync("teacher", stream, 1);

        Assert.NotNull(captured);
        Assert.Equal("Paquete externo", captured!.Title);
        Assert.Equal("Texto plano del enunciado.", captured.Description);
        Assert.Equal(string.Empty, captured.Input);
        Assert.Equal(5, captured.TimeLimit);
        Assert.Equal(1024, captured.MemoryLimit);
    }

    [Fact]
    public async Task ImportProblemPackageAsync_OrdersDoubleDigitSampleFilesNumerically()
    {
        var problemService = new Mock<IProblemService>();
        Problem? captured = null;
        problemService
            .Setup(item => item.CreateProblemAsync(It.IsAny<string>(), It.IsAny<Problem>(), It.IsAny<int>()))
            .Callback<string, Problem, int>((_, problem, _) => captured = problem)
            .ReturnsAsync((string _, Problem problem, int _) => { problem.ProblemId = 9; return problem; });
        var service = CreateService(problemService.Object);

        var zipBytes = BuildZip(entries =>
        {
            entries["data/sample/1.in"] = "one";
            entries["data/sample/1.ans"] = "one-a";
            entries["data/sample/2.in"] = "two";
            entries["data/sample/2.ans"] = "two-a";
            entries["data/sample/10.in"] = "ten";
            entries["data/sample/10.ans"] = "ten-a";
        });

        using var stream = new MemoryStream(zipBytes);
        await service.ImportProblemPackageAsync("teacher", stream, 1);

        Assert.NotNull(captured);
        var samples = captured!.SampleCases.ToList();
        Assert.Equal(3, samples.Count);
        Assert.Equal("one", samples[0].Input);
        Assert.Equal("two", samples[1].Input);
        Assert.Equal("ten", samples[2].Input);
    }

    [Fact]
    public async Task ImportProblemPackageAsync_DoesNotTouchFileSystemWhenNoSecretTestData()
    {
        var problemService = new Mock<IProblemService>();
        problemService
            .Setup(item => item.CreateProblemAsync(It.IsAny<string>(), It.IsAny<Problem>(), It.IsAny<int>()))
            .ReturnsAsync((string _, Problem problem, int _) => { problem.ProblemId = 11; return problem; });
        var fileManager = new Mock<IFileSystemLocalManagerManager>();
        var service = CreateService(problemService.Object, fileManager.Object);

        var zipBytes = BuildZip(entries =>
        {
            entries["metadata.json"] = JsonSerializer.Serialize(new { Title = "Sin test data" });
        });

        using var stream = new MemoryStream(zipBytes);
        await service.ImportProblemPackageAsync("teacher", stream, 1);

        fileManager.Verify(item => item.CreateFolder(It.IsAny<string>()), Times.Never);
        fileManager.Verify(item => item.WriteToFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ImportProblemPackageAsync_CleansUpStagingDirectory_EvenWhenCreateProblemFails()
    {
        var problemService = new Mock<IProblemService>();
        problemService
            .Setup(item => item.CreateProblemAsync(It.IsAny<string>(), It.IsAny<Problem>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var service = CreateService(problemService.Object);

        var stagingRoot = Path.Combine(Path.GetTempPath(), "icpc-import");
        var before = Directory.Exists(stagingRoot) ? Directory.GetDirectories(stagingRoot).ToHashSet() : new HashSet<string>();

        var zipBytes = BuildZip(entries => entries["metadata.json"] = JsonSerializer.Serialize(new { Title = "x" }));
        using var stream = new MemoryStream(zipBytes);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportProblemPackageAsync("teacher", stream, 1));

        var after = Directory.Exists(stagingRoot) ? Directory.GetDirectories(stagingRoot).ToHashSet() : new HashSet<string>();
        Assert.True(after.SetEquals(before), "El directorio de staging debe limpiarse incluso si CreateProblemAsync falla.");
    }

    private static IProblemService StubProblemService(int problemId, Problem problem)
    {
        var problemService = new Mock<IProblemService>();
        problemService.Setup(item => item.GetProblemByIdAsync(problemId, 1)).ReturnsAsync(problem);
        return problemService.Object;
    }

    private static Problem BuildProblem(int problemId, string title = "Título", int timeLimit = 1, int memoryLimit = 128)
    {
        return new Problem
        {
            ProblemId = problemId,
            Title = title,
            Description = "<p>desc</p>",
            Input = "<p>input</p>",
            Output = "<p>output</p>",
            Source = "Source",
            OriginSource = "OriginSource",
            Spj = "N",
            TimeLimit = timeLimit,
            MemoryLimit = memoryLimit,
            SampleCases = new List<ProblemSample>(),
            Classifications = new List<Classification>(),
        };
    }

    private static ProblemPackageService CreateService(IProblemService? problemService = null, IFileSystemLocalManagerManager? fileManager = null)
    {
        return new ProblemPackageService(
            problemService ?? Mock.Of<IProblemService>(),
            fileManager ?? DefaultFileManager());
    }

    private static IFileSystemLocalManagerManager DefaultFileManager()
    {
        var fileManager = new Mock<IFileSystemLocalManagerManager>();
        fileManager.Setup(item => item.ListFiles(It.IsAny<string>())).Returns(new List<string>());
        return fileManager.Object;
    }

    private static byte[] BuildZip(Action<Dictionary<string, string>> configure)
    {
        var entries = new Dictionary<string, string>();
        configure(entries);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static ZipArchiveEntry? FindEntry(byte[] zipBytes, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        return archive.GetEntry(entryName);
    }

    private static string ReadEntryText(byte[] zipBytes, string entryName) =>
        Encoding.UTF8.GetString(ReadEntryBytes(zipBytes, entryName));

    private static byte[] ReadEntryBytes(byte[] zipBytes, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Zip entry '{entryName}' not found.");
        using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        entryStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
