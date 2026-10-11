using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;

public class FileSystemLocalManagerManager : IFileSystemLocalManagerManager
{
    private readonly IConfiguration _configuration;
    private readonly string _path;

    public FileSystemLocalManagerManager(IConfiguration configuration)
    {
        _configuration = configuration;
        _path = _configuration["FileSettings:ProblemsFilePath"];
    }

    public void WriteToFile(string folderName, string fileName, string content)
    {
        string path = _path + Path.DirectorySeparatorChar
                    + folderName + Path.DirectorySeparatorChar + fileName;
        using (StreamWriter sw = File.CreateText(path))
        {
            sw.WriteLine(content);
        }
    }

    public void CreateFolder(string folderName)
    {
        string path = _path + Path.DirectorySeparatorChar + folderName;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public IReadOnlyList<string> ListFiles(string folderName)
    {
        string path = _path + Path.DirectorySeparatorChar + folderName;
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToList()!;
    }

    public byte[] ReadFile(string folderName, string fileName)
    {
        string path = _path + Path.DirectorySeparatorChar
                    + folderName + Path.DirectorySeparatorChar + fileName;
        return File.ReadAllBytes(path);
    }

    public void DeleteFile(string folderName, string fileName)
    {
        string path = _path + Path.DirectorySeparatorChar
                    + folderName + Path.DirectorySeparatorChar + fileName;
        File.Delete(path);
    }

    public bool FileExists(string folderName, string fileName) => File.Exists(Resolve(folderName, fileName));

    public async Task SaveFileAsync(string folderName, string fileName, Stream content)
    {
        var path = Resolve(folderName, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Create);
        await content.CopyToAsync(stream);
    }

    // Rejects any fileName that would resolve outside the folder (e.g. "../../etc/passwd") -
    // Path.Combine alone does not stop ".." segments from escaping the intended directory.
    private string Resolve(string folderName, string fileName)
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            throw new InvalidOperationException("FileSettings:ProblemsFilePath must be configured.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("El nombre del archivo es requerido.");
        }

        var folder = Path.Combine(_path, folderName);
        var fullDirectory = Path.GetFullPath(folder + Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(folder, fileName));
        if (!fullPath.StartsWith(fullDirectory, StringComparison.Ordinal))
        {
            throw new ArgumentException("Ruta de archivo inválida.");
        }

        return fullPath;
    }
}
