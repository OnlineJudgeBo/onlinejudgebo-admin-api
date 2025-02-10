namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IFileManagerService
{
    public Task<string> S3UploadFileAsync(string filePath);
    void WriteToFile(string folderName, string fileName, string content);
    void CreateFolder(string folderName);
}

