using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.Helpers;

public class FileManagerControllerTests : IDisposable
{
    private readonly string _baseDirectory;

    public FileManagerControllerTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "fm-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_baseDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDirectory))
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetFileContent_ReturnsContent_WhenFileIsWithinProblemDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "1000"));
        File.WriteAllText(Path.Combine(_baseDirectory, "1000", "statement.md"), "hello");
        var controller = CreateController(problemService: ProblemServiceReturning(1000, 1, exists: true));

        var result = await controller.GetFileContent(1000, "statement.md");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("hello", ok.Value);
    }

    [Fact]
    public async Task GetFileContent_RejectsFileNameThatEscapesProblemDirectory()
    {
        File.WriteAllText(Path.Combine(_baseDirectory, "secret.txt"), "top secret");
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "1000"));
        var controller = CreateController(problemService: ProblemServiceReturning(1000, 1, exists: true));

        await Assert.ThrowsAsync<ArgumentException>(() => controller.GetFileContent(1000, "../secret.txt"));
    }

    [Fact]
    public async Task SaveFileContentAsync_RejectsFileNameThatEscapesProblemDirectory()
    {
        var controller = CreateController(problemService: ProblemServiceReturning(1000, 1, exists: true));
        var file = BuildFormFile("payload.txt", "malicious content");

        await Assert.ThrowsAsync<ArgumentException>(() => controller.SaveFileContentAsync(1000, "../../outside.txt", file));
        Assert.False(File.Exists(Path.Combine(_baseDirectory, "outside.txt")));
    }

    [Fact]
    public async Task DeleteFile_RejectsFileNameThatEscapesProblemDirectory()
    {
        var protectedFile = Path.Combine(_baseDirectory, "keep.txt");
        File.WriteAllText(protectedFile, "do not delete me");
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "1000"));
        var controller = CreateController(problemService: ProblemServiceReturning(1000, 1, exists: true));

        await Assert.ThrowsAsync<ArgumentException>(() => controller.DeleteFile(1000, "../keep.txt"));
        Assert.True(File.Exists(protectedFile));
    }

    [Fact]
    public async Task GetFiles_RejectsProblemThatBelongsToAnotherSite()
    {
        var controller = CreateController(siteId: 1, problemService: ProblemServiceReturning(1000, 1, exists: false));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.GetFiles(1000));
    }

    [Fact]
    public async Task GetFileContent_RejectsProblemThatBelongsToAnotherSite()
    {
        var controller = CreateController(siteId: 1, problemService: ProblemServiceReturning(1000, 1, exists: false));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.GetFileContent(1000, "statement.md"));
    }

    private FileManagerController CreateController(
        int siteId = 1,
        string role = "Docente",
        IProblemService? problemService = null,
        IFileManagerService? fileManagerService = null)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(item => item[It.Is<string>(key => key == "FileSettings:ProblemsFilePath")]).Returns(_baseDirectory);

        return new FileManagerController(
            fileManagerService ?? Mock.Of<IFileManagerService>(),
            problemService ?? Mock.Of<IProblemService>(),
            Mock.Of<IMapper>(),
            CreateClaimsHelper(siteId, role),
            configuration.Object);
    }

    private static IProblemService ProblemServiceReturning(int problemId, int siteId, bool exists)
    {
        var service = new Mock<IProblemService>();
        service
            .Setup(item => item.GetProblemByIdAsync(problemId, siteId))
            .ReturnsAsync(exists ? new Problem { ProblemId = problemId } : null!);
        return service.Object;
    }

    private static UserClaimsHelper CreateClaimsHelper(int siteId, string role, string userId = "teacher")
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("site_id", siteId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(item => item.HttpContext).Returns(httpContext);
        return new UserClaimsHelper(accessor.Object);
    }

    private static IFormFile BuildFormFile(string fileName, string content)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }
}
