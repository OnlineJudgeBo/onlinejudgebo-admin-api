using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class FileSystemLocalManagerService : IFileSystemLocalManagerManager
{
    private readonly IFileSystemLocalManagerManager _userRepository;

    private readonly IValidator<Problem> _userValidation;

    public FileSystemLocalManagerService(
        IFileSystemLocalManagerManager userRepository,
        IValidator<Problem> userValidator)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
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
