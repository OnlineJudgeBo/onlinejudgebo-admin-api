
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.FileSystemManager;

public interface IFileSystemManager
{
    void WriteToFile(string folderName, string fileName, string content);
    void CreateFolder(string folderName);
}
