using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.FileSystemManager;

namespace OnlineJudgeAdmin.Infrastructure.FileSystem;

public class FileSystemManager : IFileSystemManager
{
    private readonly IConfiguration _configuration;
    private readonly string _path;

    public FileSystemManager(IConfiguration configuration)
    {
        _configuration = configuration;
        _path = _configuration["FileSettings:FilePath"];
    }

    public void WriteToFile(string content)
    {

    }

    public void CreateFolder(string content)
    {

    }
}

