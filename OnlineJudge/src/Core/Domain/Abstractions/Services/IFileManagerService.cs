namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IFileManagerService
{
    public Task<string> S3UploadFileAsync(Stream content, string fileName);
    public Task<string> UploadOfficialContestPdfAsync(string contestName, Stream content);
    void WriteToFile(string folderName, string fileName, string content);
    void CreateFolder(string folderName);
}

