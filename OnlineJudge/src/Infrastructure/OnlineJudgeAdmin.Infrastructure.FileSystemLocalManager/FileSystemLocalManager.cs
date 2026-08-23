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
}

