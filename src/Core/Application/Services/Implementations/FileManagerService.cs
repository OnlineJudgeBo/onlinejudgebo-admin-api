using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class FileManagerService : IFileManagerService
{
    private readonly IAwsS3FileManager _awsS3FileManager;
    private readonly IFileSystemLocalManagerManager _fileSystemLocalManager;
    private readonly IValidator<Problem> _userValidation;

    public FileManagerService(
        IAwsS3FileManager awsS3FileManager,
        IFileSystemLocalManagerManager fileSystemLocalManager,
        IValidator<Problem> userValidator)
    {
        _awsS3FileManager = awsS3FileManager ?? throw new ArgumentNullException(nameof(awsS3FileManager));
        _fileSystemLocalManager = fileSystemLocalManager ?? throw new ArgumentNullException(nameof(fileSystemLocalManager));

        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }

    public async Task<string> S3UploadFileAsync(string filePath)
    {
        Guid newGuid = Guid.NewGuid();
        return await _awsS3FileManager.S3UploadFileAsync("onlinejudgebo", newGuid.ToString(), filePath);
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
