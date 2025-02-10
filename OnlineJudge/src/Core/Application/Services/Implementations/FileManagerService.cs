using FluentValidation;
using Microsoft.Extensions.Configuration;
using ScheduleManager.Core.Domain.Abstractions.Infrastructure;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Services.Implementations;
public class FileManagerService : IFileManagerService
{
    private readonly IAwsS3FileManager _awsS3FileManager;
    private readonly IFileSystemLocalManagerManager _fileSystemLocalManager;
    private readonly IValidator<Problem> _userValidation;
    private readonly IConfiguration _configuration;

    public FileManagerService(
        IAwsS3FileManager awsS3FileManager,
        IFileSystemLocalManagerManager fileSystemLocalManager,
        IConfiguration configuration,
        IValidator<Problem> userValidator)
    {
        _awsS3FileManager = awsS3FileManager ?? throw new ArgumentNullException(nameof(awsS3FileManager));
        _fileSystemLocalManager = fileSystemLocalManager ?? throw new ArgumentNullException(nameof(fileSystemLocalManager));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }

    public async Task<string> S3UploadFileAsync(string filePath)
    {
        Guid newGuid = Guid.NewGuid();
        string bucketName = _configuration.GetSection("Base:BucketName").Value;
        return await _awsS3FileManager.S3UploadFileAsync(bucketName, newGuid.ToString(), filePath);
    }

    public void CreateFolder(string folderName)
    {
        throw new NotImplementedException();
    }

    public void WriteToFile(string folderName, string fileName, string content)
    {
        throw new NotImplementedException();
    }
}
