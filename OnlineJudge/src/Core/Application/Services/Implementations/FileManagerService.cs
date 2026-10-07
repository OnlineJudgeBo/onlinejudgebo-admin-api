using FluentValidation;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
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

    // The statements of an official contest live at officialContests/<contest name>.pdf in the bucket.
    public async Task<string> UploadOfficialContestPdfAsync(string contestName, string filePath)
    {
        // The name becomes an object key: keep it a single file name.
        string name = string.Concat((contestName ?? string.Empty).Where(c => !char.IsControl(c) && c is not ('/' or '\\'))).Trim();
        if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4].TrimEnd();
        }

        if (name.Length is 0 or > 200)
        {
            throw new ArgumentException("El nombre del concurso no sirve como nombre del PDF.");
        }

        string bucketName = _configuration.GetSection("Base:BucketName").Value;
        return await _awsS3FileManager.S3UploadFileAsync(bucketName, $"officialContests/{name}.pdf", filePath);
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
