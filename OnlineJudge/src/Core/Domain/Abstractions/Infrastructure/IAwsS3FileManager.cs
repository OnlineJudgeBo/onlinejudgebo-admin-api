
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IAwsS3FileManager
{
    public Task<string> S3UploadFileAsync(string bucketName, string keyName, string filePath);
}
