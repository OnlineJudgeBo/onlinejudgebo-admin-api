
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IFileSystemLocalManagerManager
{
    void WriteToFile(string folderName, string fileName, string content);
    void CreateFolder(string folderName);
}
