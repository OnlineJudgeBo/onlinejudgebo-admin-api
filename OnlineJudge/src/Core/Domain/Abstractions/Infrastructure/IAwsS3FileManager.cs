
namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IAwsS3FileManager
{
    // fileName only tells the content type, by its extension. Returns the public URL.
    public Task<string> S3UploadFileAsync(string bucketName, string keyName, Stream content, string fileName);
}
