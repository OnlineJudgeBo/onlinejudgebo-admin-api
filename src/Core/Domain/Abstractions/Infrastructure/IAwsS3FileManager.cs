
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IAwsS3FileManager
{
    public Task<string> UploadFileAsync(string bucketName, string keyName, string filePath);
}
