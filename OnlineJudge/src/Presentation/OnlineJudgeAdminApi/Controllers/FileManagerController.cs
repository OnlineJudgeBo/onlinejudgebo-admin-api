using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
namespace OnlineJudgeAdminApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]

public class FileManagerController : ControllerBase
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IMapper _mapper;
    private readonly string baseDirectory = @"/tmp/zas/";
    public FileManagerController(IFileManagerService fileManagerService, IMapper mapper)
    {
        _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpPost("cloud-storage")]
    public async Task<IActionResult> S3UploadFileContentAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Content("No file uploaded.");
        }

        var path = Path.Combine(Path.GetTempPath(), "", file.FileName);

        using (var stream = new FileStream(path, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(await _fileManagerService.S3UploadFileAsync(path));
    }

    [HttpGet("local-storage")]
    public IActionResult GetFiles(int problemId)
    {
        var result = GetDirectoryContents(baseDirectory + problemId + "");
        return Ok(result);
    }

    [HttpGet("local-storage/ac")]
    public IActionResult GetFilesAc(int problemId)
    {
        var result = GetDirectoryContents(baseDirectory + problemId + "/ac");
        return Ok(result);
    }

    private static object GetDirectoryContents(string path, string rootPath = null)
    {
        Console.WriteLine($"Getting directory contents for path: {path}.");
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
    public IActionResult GetFileContent(int problemId, string fileName)
    {
        var filePath = Path.Combine(baseDirectory, problemId.ToString(), fileName);
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

        var filePath = Path.Combine(baseDirectory, problemId.ToString(), fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        return Ok(new { file.FileName, file.Length });
    }

    [HttpDelete("local-storage")]
    public IActionResult DeleteFile(int problemId, string fileName)
    {
        var filePath = Path.Combine(baseDirectory, problemId.ToString(), fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        System.IO.File.Delete(filePath);
        return Ok();
    }
}
