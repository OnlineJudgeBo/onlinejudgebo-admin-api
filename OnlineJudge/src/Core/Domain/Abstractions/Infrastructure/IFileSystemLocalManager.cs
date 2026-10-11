
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IFileSystemLocalManagerManager
{
    void WriteToFile(string folderName, string fileName, string content);
    void CreateFolder(string folderName);

    // Excludes the "ac" subfolder (accepted-submission cache, not test data) by design -
    // every caller of ListFiles wants the real judge data set, never that cache.
    IReadOnlyList<string> ListFiles(string folderName);
    byte[] ReadFile(string folderName, string fileName);

    // No-op if the file doesn't exist, matching File.Delete's own semantics.
    void DeleteFile(string folderName, string fileName);

    // The two below reject a fileName that would leave the folder (ArgumentException).
    bool FileExists(string folderName, string fileName);

    // fileName may include subfolders; they are created.
    Task SaveFileAsync(string folderName, string fileName, Stream content);
}
