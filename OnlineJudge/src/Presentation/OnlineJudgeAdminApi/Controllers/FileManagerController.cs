using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;
namespace OnlineJudgeAdminApi.Controllers;

[Route("/api/[controller]")]
[ApiController]
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]

public class FileManagerController : ControllerBase
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IProblemService _problemService;
    private readonly IMapper _mapper;
    private readonly CurrentUser _currentUser;
    private readonly string baseDirectory;

    public FileManagerController(
        IFileManagerService fileManagerService,
        IProblemService problemService,
        IMapper mapper,
        UserClaimsHelper userClaimsHelper,
        IConfiguration configuration)
    {
        _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = (userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper))).GetUserContextRole();
        // Same location IFileSystemLocalManagerManager (and this file's own writes) use -
        // this used to be hardcoded to /tmp/zas, disconnected from where problem files
        // (including test data) actually live, so this page always showed empty.
        baseDirectory = configuration["FileSettings:ProblemsFilePath"]
            ?? throw new InvalidOperationException("FileSettings:ProblemsFilePath must be configured.");
    }

    [HttpPost("cloud-storage")]
    public async Task<IActionResult> S3UploadFileContentAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Content("No file uploaded.");
        }

        var path = Path.Combine(Path.GetTempPath(), Path.GetFileName(file.FileName));

        using (var stream = new FileStream(path, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(await _fileManagerService.S3UploadFileAsync(path));
    }

    [HttpGet("local-storage")]
    public async Task<IActionResult> GetFiles(int problemId)
    {
        await EnsureProblemBelongsToSiteAsync(problemId);
        var result = GetDirectoryContents(GetProblemDirectory(problemId));
        return Ok(result);
    }

    [HttpGet("local-storage/ac")]
    public async Task<IActionResult> GetFilesAc(int problemId)
    {
        await EnsureProblemBelongsToSiteAsync(problemId);
        var result = GetDirectoryContents(Path.Combine(GetProblemDirectory(problemId), "ac"));
        return Ok(result);
    }

    private async Task EnsureProblemBelongsToSiteAsync(int problemId)
    {
        var problem = await _problemService.GetProblemByIdAsync(problemId, _currentUser.SiteId);
        if (problem == null)
        {
            throw new KeyNotFoundException("Problem not found with ID: " + problemId);
        }
    }

    private string GetProblemDirectory(int problemId)
    {
        return Path.Combine(baseDirectory, problemId.ToString());
    }

    // Rejects any fileName that would resolve outside problemDirectory (e.g. "../../etc/passwd") -
    // Path.Combine alone does not stop ".." segments from escaping the intended directory.
    private static string ResolveFilePath(string problemDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.");
        }

        var fullDirectory = Path.GetFullPath(problemDirectory + Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(problemDirectory, fileName));

        if (!fullPath.StartsWith(fullDirectory, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid file path.");
        }

        return fullPath;
    }

    private static object GetDirectoryContents(string path, string? rootPath = null)
    {
        Console.WriteLine($"Getting directory contents for path: {path}.");
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"Directory does not exist for path: {path}.");
            return Array.Empty<object>();
        }

        DirectoryInfo directoryInfo = new DirectoryInfo(path);
        rootPath ??= path;

        var directoryContents = directoryInfo.GetFiles()
            .Select(file => new
            {
                Name = file.Name,
                Type = "file",
                Path = file.FullName.Substring(rootPath.Length).Replace("\\", "/"),
                Children = Array.Empty<object>()
            })
            .OrderBy(x => x.Name)
            .Cast<object>()
            .ToList();

        Console.WriteLine($"Directory contents retrieved for path: {path}.");
        return directoryContents;
    }

    [HttpGet("local-storage/content")]
    public async Task<IActionResult> GetFileContent(int problemId, string fileName)
    {
        await EnsureProblemBelongsToSiteAsync(problemId);
        var filePath = ResolveFilePath(GetProblemDirectory(problemId), fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var content = System.IO.File.ReadAllText(filePath);
        return Ok(content);
    }

    [HttpPost("local-storage")]
    public async Task<IActionResult> SaveFileContentAsync(int problemId, string fileName, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Upload a file.");
        }

        await EnsureProblemBelongsToSiteAsync(problemId);

        var directoryPath = GetProblemDirectory(problemId);
        var filePath = ResolveFilePath(directoryPath, fileName);
        Directory.CreateDirectory(directoryPath);

        var targetDirectory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        return Ok(new { file.FileName, file.Length });
    }

    [HttpDelete("local-storage")]
    public async Task<IActionResult> DeleteFile(int problemId, string fileName)
    {
        await EnsureProblemBelongsToSiteAsync(problemId);
        var filePath = ResolveFilePath(GetProblemDirectory(problemId), fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        System.IO.File.Delete(filePath);
        return Ok();
    }
}
