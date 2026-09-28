using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using static ControllerTestSupport;

public class ContestPackageServiceTests
{
    private readonly Mock<IContestService> _contests = new();
    private readonly Mock<IProblemPackageService> _problems = new();
    private readonly Mock<IProgrammingLanguageService> _languages = new();

    private ContestPackageService Service() => new(_contests.Object, _problems.Object, _languages.Object);

    [Fact]
    public async Task Package_RoundTripsMetadataLanguagesAndProblemOrder()
    {
        _contests.Setup(item => item.GetContestById(7, 3)).ReturnsAsync(new Contest
        {
            ContestId = 7,
            Title = "Final nacional",
            Description = "Descripción",
            StartTime = new DateTime(2026, 10, 1, 8, 0, 0),
            EndTime = new DateTime(2026, 10, 1, 13, 0, 0),
            Defunct = "N",
            Private = 1,
            IsExam = true,
            ExamLabIps = "192.0.2.0/24",
            ContestProblems = [new() { ProblemId = 20, Num = 1 }, new() { ProblemId = 10, Num = 0 }],
            ProgrammingLanguages = [new() { LanguageId = 4, Name = "C++" }],
        });
        _problems.Setup(item => item.ExportProblemPackageAsync(It.IsAny<int>(), 3))
            .ReturnsAsync((int id, int _) => System.Text.Encoding.UTF8.GetBytes($"problem-{id}"));
        var package = await Service().ExportAsync(7, 3);

        var nextId = 100;
        _problems.Setup(item => item.ImportProblemPackageAsync("admin", It.IsAny<Stream>(), 3))
            .ReturnsAsync(() => new Problem { ProblemId = nextId++ });
        _languages.Setup(item => item.GetAllProgrammingLanguageAsync())
            .ReturnsAsync([new ProgrammingLanguage { LanguageId = 9, Name = "C++" }, new ProgrammingLanguage { LanguageId = 10, Name = "Java" }]);
        Contest? created = null;
        _contests.Setup(item => item.CreateContestAsync("admin", It.IsAny<Contest>(), string.Empty, 3))
            .Callback<string, Contest, string, int>((_, contest, _, _) => created = contest)
            .ReturnsAsync(() => { created!.ContestId = 50; return created; });

        var result = await Service().ImportAsync("admin", new MemoryStream(package), 3);

        Assert.Equal(50, result.ContestId);
        Assert.Equal("Final nacional", created!.Title);
        Assert.True(created.IsExam);
        Assert.Null(created.ExamLabIps);
        Assert.Equal([100, 101], created.ContestProblems.Select(item => item.ProblemId));
        Assert.Equal(9, Assert.Single(created.ProgrammingLanguages!).LanguageId);
        _problems.Verify(item => item.ExportProblemPackageAsync(10, 3), Times.Once);
        _problems.Verify(item => item.ExportProblemPackageAsync(20, 3), Times.Once);
    }

    [Fact]
    public async Task Import_RejectsPackageWithoutManifest()
    {
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("problem.zip");
        }
        bytes.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => Service().ImportAsync("admin", bytes, 1));
    }
}

public class ContestPackagesControllerTests
{
    [Fact]
    public async Task EmptyImportIsRejected()
    {
        var context = AuthenticatedContext("admin", 2, "Administrador");
        var packages = new ContestPackageService(Mock.Of<IContestService>(), Mock.Of<IProblemPackageService>(), Mock.Of<IProgrammingLanguageService>());
        var controller = new ContestPackagesController(packages, Claims(context)).WithContext(context);
        var empty = new FormFile(new MemoryStream(), 0, 0, "file", "empty.zip");

        Assert.IsType<BadRequestObjectResult>(await controller.ImportAsync(empty));
    }
}
