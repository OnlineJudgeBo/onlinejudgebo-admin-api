
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.FileSystemManager;

public interface IFileSystemManager
{
    void CreateFolder(string folderName);
    void WriteToFile(string content);
}

