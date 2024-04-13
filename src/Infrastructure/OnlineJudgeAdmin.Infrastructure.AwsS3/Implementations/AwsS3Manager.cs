using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.AwsS3.Implementations;
public class AwsS3Manager: IAwsS3FileManager
{
    private readonly IConfiguration _configuration;
    private readonly string _path;

    public AwsS3Manager(IConfiguration configuration)
    {
        _configuration = configuration;
        _path = _configuration["FileSettings:ProblemsFilePath"];
    }

    public async Task<string> UploadFileAsync(string bucketName, string keyName, string filePath)
    {
        throw new NotImplementedException();
    }
}

