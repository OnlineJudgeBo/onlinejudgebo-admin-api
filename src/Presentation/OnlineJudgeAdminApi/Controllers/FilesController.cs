using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly string baseDirectory = @"/tmp/zas/";


    [HttpGet]
    public IActionResult GetFiles(int problemId)
    {
        var result = GetDirectoryContents(baseDirectory + problemId + "");
        return Ok(result);
    }

    private object GetDirectoryContents(string path, string rootPath = null)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(path);
        rootPath ??= path;

        var directoryContents = directoryInfo.GetDirectories()
            .Select(dir => new
            {
                Name = dir.Name,
                Type = "directory",
                Path = dir.FullName.Substring(rootPath.Length).Replace("\\", "/"),
                Children = GetDirectoryContents(dir.FullName, rootPath)
            })
            .Cast<object>()
            .Concat(directoryInfo.GetFiles().Select(file => new
            {
                Name = file.Name,
                Type = "file",
                Path = file.FullName.Substring(rootPath.Length).Replace("\\", "/"),
                Children = new object[0]
            })
            .Cast<object>())
            .ToList();

        return directoryContents;
    }


    [HttpGet("content")]
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

    [HttpPost]
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

    [HttpDelete]
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
