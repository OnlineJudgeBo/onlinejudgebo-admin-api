using OnlineJudgeAdmin.Core.Domain.Models;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.BocaImporter;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using static ControllerTestSupport;

// Builds BOCA packages on disk: description/problem.info, input/*, output/*, limits/*.
internal sealed class BocaPackage : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("boca-test-").FullName;

    public BocaPackage()
    {
        foreach (var dir in new[] { "description", "input", "output", "limits" })
        {
            Directory.CreateDirectory(Path.Combine(Root, dir));
        }
    }

    public BocaPackage File(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return this;
    }

    public BocaPackage Info(string content) => File("description/problem.info", content);

    public BocaPackage Case(string name, string input, string output) => File("input/" + name, input).File("output/" + name, output);

    public BocaPackage Limit(string language, int timeSeconds, int memoryMb) =>
        File("limits/" + language, $"echo {timeSeconds}\necho 1\necho {memoryMb}\necho 1024\n");

    public BocaPackage Valid(string title = "Suma") =>
        Info($"basename=suma\nfullname={title}\ndescfile=desc.txt").File("description/desc.txt", "Lee dos enteros.\n\nImprime su suma <b>.").Case("1", "1 2\n", "3\n").Limit("c", 1, 256);

    public byte[] Zip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
            {
                archive.CreateEntryFromFile(file, Path.GetRelativePath(Root, file));
            }
        }

        return stream.ToArray();
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

public class BocaPackageReaderTests
{
    [Fact]
    public void ReadProblemInfo_ParsesKeyValuesAndTrims()
    {
        using var package = new BocaPackage().Info("# comment\nbasename = suma \n fullname = Suma de enteros \ndescfile=desc.txt\ninvalid line\n=novalue");

        var info = BocaPackageReader.ReadProblemInfo(package.Root, "folder");

        Assert.Equal("folder", info.BaseName);
        Assert.Equal("Suma de enteros", info.FullName);
        Assert.Equal("desc.txt", info.DescFile);
    }

    [Fact]
    public void ReadProblemInfo_FallsBackToOnlyDescriptionFile()
    {
        using var package = new BocaPackage().Info("fullname=Suma").File("description/enunciado.pdf", "%PDF");

        Assert.Equal("enunciado.pdf", BocaPackageReader.ReadProblemInfo(package.Root, "x").DescFile);
    }

    [Fact]
    public void ReadProblemInfo_NullDescFileWhenNoneExists()
    {
        using var package = new BocaPackage().Info("fullname=Suma");

        Assert.Null(BocaPackageReader.ReadProblemInfo(package.Root, "x").DescFile);
    }

    [Fact]
    public void ReadProblemInfo_RequiresFullNameWithinTitleLength()
    {
        using var missing = new BocaPackage().Info("basename=suma");
        using var tooLong = new BocaPackage().Info("fullname=" + new string('a', 201));
        using var maxLength = new BocaPackage().Info("fullname=" + new string('a', 200));

        Assert.Contains("missing fullname", Assert.Throws<InvalidOperationException>(() => BocaPackageReader.ReadProblemInfo(missing.Root, "x")).Message);
        Assert.Contains("201 characters", Assert.Throws<InvalidOperationException>(() => BocaPackageReader.ReadProblemInfo(tooLong.Root, "x")).Message);
        Assert.Equal(200, BocaPackageReader.ReadProblemInfo(maxLength.Root, "x").FullName.Length);
    }

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("../secret.txt")]
    [InlineData("sub/desc.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("..")]
    public void ReadProblemInfo_RejectsDescFileOutsideDescriptionFolder(string descFile)
    {
        using var package = new BocaPackage().Info("fullname=Suma\ndescfile=" + descFile).File("secret.txt", "JWT_KEY=top-secret");

        var error = Assert.Throws<InvalidOperationException>(() => BocaPackageReader.ReadProblemInfo(package.Root, "x"));

        Assert.Contains("descfile must be a file name", error.Message);
    }

    [Fact]
    public async Task ReadDescription_TextBecomesEncodedParagraphs()
    {
        using var package = new BocaPackage().File("description/desc.txt", "Linea <uno>\ncontinua\n\n\n  Segundo & final  \n");

        var description = await BocaPackageReader.ReadDescriptionAsync(package.Root, "desc.txt");

        Assert.Equal("<p>Linea &lt;uno&gt;\ncontinua</p>\n<p>Segundo &amp; final</p>", description.Html);
        Assert.False(description.NeedsReview);
    }

    [Fact]
    public async Task ReadDescription_FlagsMissingOrEmptyFiles()
    {
        using var package = new BocaPackage().File("description/empty.txt", "  \n\n ");

        var none = await BocaPackageReader.ReadDescriptionAsync(package.Root, null);
        var empty = await BocaPackageReader.ReadDescriptionAsync(package.Root, "empty.txt");

        Assert.True(none.NeedsReview);
        Assert.Equal("no description file found", none.ReviewReason);
        Assert.Equal("description file is empty", empty.ReviewReason);
    }

    [Fact]
    public void ReadSamplePair_PicksSmallestPairedCase()
    {
        using var package = new BocaPackage()
            .Case("big", new string('9', 500), "big-out")
            .Case("small", "1 2", "3")
            .File("input/orphan", "x");

        var sample = BocaPackageReader.ReadSamplePair(package.Root);

        Assert.Equal("1 2", sample.Input);
        Assert.Equal("3", sample.Output);
        Assert.False(sample.NeedsReview);
    }

    [Fact]
    public void ReadSamplePair_TruncatesHugeCasesAndFlagsReview()
    {
        using var package = new BocaPackage().Case("only", new string('1', 20_001), "ok");

        var sample = BocaPackageReader.ReadSamplePair(package.Root);

        Assert.True(sample.NeedsReview);
        Assert.EndsWith("...(truncated by importer)", sample.Input);
        Assert.Equal("ok", sample.Output);
    }

    [Fact]
    public void ReadSamplePair_RequiresAtLeastOnePair()
    {
        using var package = new BocaPackage().File("input/a", "1").File("output/b", "2");

        Assert.Throws<InvalidOperationException>(() => BocaPackageReader.ReadSamplePair(package.Root));
    }

    [Fact]
    public void ReadAllTestCases_ReturnsEveryPairInNameOrderUntruncated()
    {
        using var package = new BocaPackage().Case("2", "b", "B").Case("1", new string('x', 30_000), "A").File("output/3", "orphan");

        var cases = BocaPackageReader.ReadAllTestCases(package.Root);

        Assert.Equal(2, cases.Count);
        Assert.Equal(30_000, cases[0].Input.Length);
        Assert.Equal("B", cases[1].Output);
    }

    [Fact]
    public void CollectLimits_TakesMaxAcrossLanguagesAndSkipsBadScripts()
    {
        using var package = new BocaPackage()
            .Limit("c", 2, 256)
            .Limit("java", 5, 512)
            .File("limits/broken", "exit 3\n")
            .File("limits/garbage", "echo not-a-number\necho 1\necho x\n");

        var limits = BocaPackageReader.CollectLimits(package.Root);

        Assert.Equal(5, limits.TimeLimitSeconds);
        Assert.Equal(512, limits.MemoryLimitMb);
    }

    [Fact]
    public void CollectLimits_DefaultsWhenNothingUsableAndKeepsMemoryFloor()
    {
        using var empty = new BocaPackage();
        using var lowMemory = new BocaPackage().Limit("c", 3, 64);

        Assert.Equal(new BocaLimits(1, 128), BocaPackageReader.CollectLimits(empty.Root));
        Assert.Equal(new BocaLimits(3, 128), BocaPackageReader.CollectLimits(lowMemory.Root));
    }
}

public class BocaImportControllerPreviewTests
{
    private static BocaImportController Controller(Mock<IProblemService>? problems = null, ProblemClassificationSuggestion? suggestion = null)
    {
        var context = AuthenticatedContext("teacher", 1, "Docente");
        var classifier = new Mock<IProblemClassifierService>();
        classifier.Setup(item => item.SuggestClassificationsAsync(It.IsAny<Problem>())).ReturnsAsync(suggestion ?? new ProblemClassificationSuggestion());
        return new BocaImportController((problems ?? new Mock<IProblemService>()).Object, classifier.Object, Mock.Of<IFileSystemLocalManagerManager>(), Claims(context)).WithContext(context);
    }

    private static IFormFile Upload(byte[] bytes, string name = "suma.zip") => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", name);

    private static IEnumerable<T> Results<T>(IActionResult result) =>
        (IEnumerable<T>)OkValue(result)!.GetType().GetProperty("results")!.GetValue(OkValue(result))!;

    [Fact]
    public async Task Preview_IncludesWhyEachClassificationWasSuggested()
    {
        using var package = new BocaPackage().Valid("Suma de enteros").Case("2", "5 5", "10");
        var suggestion = new ProblemClassificationSuggestion
        {
            Available = true,
            Classifications = new List<Classification> { new() { ClassificationId = 65, Name = "Aritmética básica", Topic = new Topic { Name = "Matemáticas" } } },
            Reasons = new Dictionary<int, string> { [65] = "Solo hay que sumar dos enteros." },
        };

        var item = Assert.Single(Results<BocaImportPreviewResult>(await Controller(suggestion: suggestion).Preview(new List<IFormFile> { Upload(package.Zip()) })));

        var suggested = Assert.Single(item.SuggestedClassifications);
        Assert.Equal("Matemáticas > Aritmética básica", suggested.Label);
        Assert.Equal("Solo hay que sumar dos enteros.", suggested.Reason);
        Directory.Delete(Path.Combine(Path.GetTempPath(), "boca-import", item.StagingId!), recursive: true);
    }

    [Fact]
    public async Task Preview_ReadsPackageAndReturnsStagingId()
    {
        using var package = new BocaPackage().Valid("Suma de enteros").Case("2", "5 5", "10");

        var item = Assert.Single(Results<BocaImportPreviewResult>(await Controller().Preview(new List<IFormFile> { Upload(package.Zip()) })));

        Assert.True(item.Success, item.Error);
        Assert.True(Guid.TryParseExact(item.StagingId, "N", out _));
        Assert.Equal("Suma de enteros", item.Title);
        Assert.Equal(1, item.TimeLimit);
        Assert.Equal(256, item.MemoryLimit);
        Assert.Equal(2, item.TestCaseCount);
        Assert.False(item.NeedsReview);
        Assert.StartsWith("<p>Lee dos enteros.</p>", item.DescriptionPreview);
        Directory.Delete(Path.Combine(Path.GetTempPath(), "boca-import", item.StagingId!), recursive: true);
    }

    [Fact]
    public async Task Preview_ReportsReviewReasonsAndPerFileFailures()
    {
        using var noDescription = new BocaPackage().Info("fullname=Sin enunciado").Case("1", "1", "1").Limit("c", 1, 128);
        using var broken = new BocaPackage().Info("basename=x");

        var results = Results<BocaImportPreviewResult>(await Controller().Preview(new List<IFormFile>
        {
            Upload(noDescription.Zip(), "a.zip"),
            Upload(broken.Zip(), "b.zip"),
            Upload(Encoding.UTF8.GetBytes("not a zip"), "c.zip"),
        })).ToList();

        Assert.True(results[0].Success);
        Assert.True(results[0].NeedsReview);
        Assert.Contains("description: no description file found", results[0].ReviewReasons);
        Assert.False(results[1].Success);
        Assert.Contains("missing fullname", results[1].Error);
        Assert.False(results[2].Success);
        Assert.Null(results[2].StagingId);
        Directory.Delete(Path.Combine(Path.GetTempPath(), "boca-import", results[0].StagingId!), recursive: true);
    }

    [Fact]
    public async Task Preview_DoesNotLeakServerFilesThroughDescFile()
    {
        var secret = Path.Combine(Path.GetTempPath(), "boca-secret-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(secret, "JWT_KEY=top-secret");
        try
        {
            using var package = new BocaPackage().Info("fullname=Leak\ndescfile=../../../../../../../.." + secret).Case("1", "1", "1");

            var item = Assert.Single(Results<BocaImportPreviewResult>(await Controller().Preview(new List<IFormFile> { Upload(package.Zip()) })));

            Assert.False(item.Success);
            Assert.DoesNotContain("top-secret", item.DescriptionPreview ?? string.Empty);
        }
        finally
        {
            File.Delete(secret);
        }
    }

    [Fact]
    public async Task Confirm_ReportsExpiredStagingWithoutCreatingProblem()
    {
        var problems = new Mock<IProblemService>();

        var result = await Controller(problems).Confirm(new BocaImportConfirmRequest { StagingIds = new List<string> { Guid.NewGuid().ToString("N") } });

        var item = Assert.Single(Results<BocaImportConfirmResult>(result));
        Assert.False(item.Success);
        Assert.Contains("vuelve a subirlo", item.Error);
        problems.VerifyNoOtherCalls();
    }
}
