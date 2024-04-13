using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class AwsS3FileManagerService : IAwsS3FileManager
{
    private readonly IAwsS3FileManager _awsS3FileManager;

    private readonly IValidator<Problem> _userValidation;

    public AwsS3FileManagerService(
        IAwsS3FileManager awsS3FileManager,
        IValidator<Problem> userValidator)
    {
        _awsS3FileManager = awsS3FileManager ?? throw new ArgumentNullException(nameof(awsS3FileManager));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }

    public Task<string> UploadFileAsync(string bucketName, string keyName, string filePath)
    {
        throw new NotImplementedException();
    }
}
