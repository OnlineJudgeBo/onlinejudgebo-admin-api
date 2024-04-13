using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
namespace OnlineJudgeAdminApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileManagerController : ControllerBase
{
    private readonly IAwsS3FileManager _awsS3FileManager;
    private readonly IMapper _mapper;

    public FileManagerController(IAwsS3FileManager awsS3FileManager, IMapper mapper)
    {
        _awsS3FileManager = awsS3FileManager ?? throw new ArgumentNullException(nameof(awsS3FileManager));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpPost]
    public async Task<IActionResult> SaveFileContentAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Upload a file.");
        }

        await _awsS3FileManager.UploadFileAsync("bucketName", "keyName", "filePath");
        return Ok();
    }
}
