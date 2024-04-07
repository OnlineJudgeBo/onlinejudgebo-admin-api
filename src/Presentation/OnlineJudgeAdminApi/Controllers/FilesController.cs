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
                Path = dir.FullName.Substring(rootPath.Length).Replace("\\", "/"), // Ajusta el path para hacerlo relativo y usar slashes
                Children = GetDirectoryContents(dir.FullName, rootPath) // Llamada recursiva para obtener contenidos del subdirectorio
            })
            .Cast<object>() // Cast para tratar todo como el mismo tipo
            .Concat(directoryInfo.GetFiles().Select(file => new
            {
                Name = file.Name,
                Type = "file",
                Path = file.FullName.Substring(rootPath.Length).Replace("\\", "/"), // Ajusta el path para hacerlo relativo
                Children = new object[0] // Agrega Children como una lista vacía
            })
            .Cast<object>()) // Nuevamente, cast para unificar los tipos
            .ToList();

        return directoryContents;
    }


    [HttpGet("content")]
    public IActionResult GetFileContent(string path)
    {
        var filePath = Path.Combine(baseDirectory, path);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var content = System.IO.File.ReadAllText(filePath);
        return Ok(content);
    }

    [HttpPost("save")]
    public IActionResult SaveFileContent(string path, [FromBody] string content)
    {
        var filePath = Path.Combine(baseDirectory, path);
        System.IO.File.WriteAllText(filePath, content);
        return Ok();
    }

    [HttpDelete("delete")]
    public IActionResult DeleteFile(string path)
    {
        var filePath = Path.Combine(baseDirectory, path);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        System.IO.File.Delete(filePath);
        return Ok();
    }
}
