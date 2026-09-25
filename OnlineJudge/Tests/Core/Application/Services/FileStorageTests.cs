using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using static ControllerTestSupport;

public sealed class FileManagerControllerStorageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("problems-").FullName;
    private readonly Mock<IFileManagerService> _files = new();
    private readonly Mock<IProblemService> _problems = new();

    public FileManagerControllerStorageTests()
    {
        _problems.Setup(item => item.GetProblemByIdAsync(1000, 1)).ReturnsAsync(new Problem { ProblemId = 1000 });
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FileManagerController Controller(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?> { ["FileSettings:ProblemsFilePath"] = _root }).Build();
        return new FileManagerController(_files.Object, _problems.Object, ApiMapper, Claims(AuthenticatedContext("teacher", 1, "Docente")), configuration);
    }

    private static IFormFile Upload(string content, string name = "1.in") =>
        new FormFile(new MemoryStream(Encoding.UTF8.GetBytes(content)), 0, Encoding.UTF8.GetByteCount(content), "file", name);

    [Fact]
    public void Constructor_RequiresProblemsPathAndDependencies()
    {
        Assert.Throws<InvalidOperationException>(() => Controller(new Dictionary<string, string?>()));
        var configuration = new ConfigurationBuilder().Build();
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new FileManagerController(null!, _problems.Object, ApiMapper, claims, configuration));
        Assert.Throws<ArgumentNullException>(() => new FileManagerController(_files.Object, null!, ApiMapper, claims, configuration));
        Assert.Throws<ArgumentNullException>(() => new FileManagerController(_files.Object, _problems.Object, null!, claims, configuration));
        Assert.Throws<ArgumentNullException>(() => new FileManagerController(_files.Object, _problems.Object, ApiMapper, null!, configuration));
    }

    [Fact]
    public async Task SaveListReadAndDelete_RoundTripInsideProblemFolder()
    {
        var controller = Controller();

        Assert.IsType<OkObjectResult>(await controller.SaveFileContentAsync(1000, "2.out", Upload("42")));
        Assert.IsType<OkObjectResult>(await controller.SaveFileContentAsync(1000, "ac/sol.cpp", Upload("int main(){}", "sol.cpp")));
        Assert.IsType<OkObjectResult>(await controller.SaveFileContentAsync(1000, "1.in", Upload("7")));

        var listing = OkValue(await controller.GetFiles(1000)) as IEnumerable<object>;
        var names = listing!.Select(item => item.GetType().GetProperty("Name")!.GetValue(item)).ToList();
        var acListing = OkValue(await controller.GetFilesAc(1000)) as IEnumerable<object>;

        Assert.Equal(new object[] { "1.in", "2.out" }, names);
        Assert.Single(acListing!);
        Assert.Equal("42", OkValue(await controller.GetFileContent(1000, "2.out")));
        Assert.IsType<OkResult>(await controller.DeleteFile(1000, "2.out"));
        Assert.IsType<NotFoundResult>(await controller.GetFileContent(1000, "2.out"));
        Assert.IsType<NotFoundResult>(await controller.DeleteFile(1000, "2.out"));
    }

    [Fact]
    public async Task GetFiles_EmptyWhenFolderMissing()
    {
        var listing = OkValue(await Controller().GetFiles(1000)) as IEnumerable<object>;

        Assert.Empty(listing!);
    }

    [Fact]
    public async Task Save_RejectsEmptyUpload()
    {
        Assert.IsType<BadRequestObjectResult>(await Controller().SaveFileContentAsync(1000, "1.in", Upload("")));
        Assert.IsType<BadRequestObjectResult>(await Controller().SaveFileContentAsync(1000, "1.in", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("../1001/1.in")]
    [InlineData("/etc/passwd")]
    public async Task FileOperations_RejectBlankOrEscapingNames(string fileName)
    {
        var controller = Controller();

        await Assert.ThrowsAsync<ArgumentException>(() => controller.GetFileContent(1000, fileName));
        await Assert.ThrowsAsync<ArgumentException>(() => controller.DeleteFile(1000, fileName));
    }

    [Fact]
    public async Task S3Upload_RequiresFileAndDelegates()
    {
        _files.Setup(item => item.S3UploadFileAsync(It.IsAny<string>())).ReturnsAsync("https://bucket/1.png");
        var controller = Controller();

        Assert.IsType<ContentResult>(await controller.S3UploadFileContentAsync(null!));
        Assert.Equal("https://bucket/1.png", OkValue(await controller.S3UploadFileContentAsync(Upload("png", "1.png"))));
    }
}

public sealed class FileSystemLocalManagerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("judge-data-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FileSystemLocalManagerManager Manager() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FileSettings:ProblemsFilePath"] = _root }).Build());

    [Fact]
    public void CreateWriteListReadDelete()
    {
        var manager = Manager();

        manager.CreateFolder("1000");
        manager.CreateFolder("1000");
        manager.WriteToFile("1000", "1.in", "1 2");
        manager.WriteToFile("1000", "1.out", "3");

        Assert.Equal(new[] { "1.in", "1.out" }, manager.ListFiles("1000").OrderBy(name => name));
        Assert.Equal("1 2" + Environment.NewLine, Encoding.UTF8.GetString(manager.ReadFile("1000", "1.in")));
        manager.DeleteFile("1000", "1.in");
        Assert.Equal(new[] { "1.out" }, manager.ListFiles("1000"));
        Assert.Empty(manager.ListFiles("missing"));
    }
}
