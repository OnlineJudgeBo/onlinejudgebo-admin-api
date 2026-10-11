using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IProblemFilesService _problemFiles;
    private readonly IMapper _mapper;
    private readonly CurrentUser _currentUser;

    public FileManagerController(
        IFileManagerService fileManagerService,
        IProblemFilesService problemFiles,
        IMapper mapper,
        UserClaimsHelper userClaimsHelper)
    {
        _fileManagerService = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
        _problemFiles = problemFiles ?? throw new ArgumentNullException(nameof(problemFiles));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = (userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper))).GetUserContextRole();
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

    // PDF with every statement of an official contest, stored under the contest name.
    [HttpPost("cloud-storage/official-contest")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public async Task<IActionResult> UploadOfficialContestPdfAsync(IFormFile file, [FromForm] string name)
    {
        var header = new byte[5];
        if (file == null || file.Length < header.Length)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = "Sube un archivo PDF." });
        }

        // The object is published as-is, so check the content and not just the extension.
        await using (var content = file.OpenReadStream())
        {
            await content.ReadExactlyAsync(header);
        }

        if (!header.AsSpan().SequenceEqual("%PDF-"u8))
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = "El archivo no es un PDF." });
        }

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { url = await _fileManagerService.UploadOfficialContestPdfAsync(name, path) });
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [HttpGet("local-storage")]
    public async Task<IActionResult> GetFiles(int problemId)
    {
        return Ok(Listing(await _problemFiles.ListAsync(problemId, _currentUser.SiteId)));
    }

    [HttpGet("local-storage/ac")]
    public async Task<IActionResult> GetFilesAc(int problemId)
    {
        return Ok(Listing(await _problemFiles.ListAsync(problemId, _currentUser.SiteId, accepted: true)));
    }

    private static List<object> Listing(IReadOnlyList<string> names) => names
        .Select(name => (object)new { Name = name, Type = "file", Path = "/" + name, Children = Array.Empty<object>() })
        .ToList();

    [HttpGet("local-storage/content")]
    public async Task<IActionResult> GetFileContent(int problemId, string fileName)
    {
        var content = await _problemFiles.ReadAsync(problemId, _currentUser.SiteId, fileName);
        return content == null ? NotFound() : Ok(content);
    }

    [HttpPost("local-storage")]
    public async Task<IActionResult> SaveFileContentAsync(int problemId, string fileName, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Upload a file.");
        }

        await using var content = file.OpenReadStream();
        await _problemFiles.SaveAsync(problemId, _currentUser.SiteId, fileName, content);
        return Ok(new { file.FileName, file.Length });
    }

    [HttpDelete("local-storage")]
    public async Task<IActionResult> DeleteFile(int problemId, string fileName)
    {
        return await _problemFiles.DeleteAsync(problemId, _currentUser.SiteId, fileName) ? Ok() : NotFound();
    }
}
