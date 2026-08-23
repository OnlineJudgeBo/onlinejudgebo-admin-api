
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IFileSystemLocalManagerManager
{
    void WriteToFile(string folderName, string fileName, string content);
    void CreateFolder(string folderName);

    // Excludes the "ac" subfolder (accepted-submission cache, not test data) by design -
    // every caller of ListFiles wants the real judge data set, never that cache.
    IReadOnlyList<string> ListFiles(string folderName);
    byte[] ReadFile(string folderName, string fileName);
}
