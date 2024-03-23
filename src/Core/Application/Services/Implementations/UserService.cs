using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    private readonly IValidator<Problem> _userValidation;

    public UserService(
        IUserRepository userRepository,
        IValidator<Problem> userValidator)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }

    public async Task<IEnumerable<User>> GetAllUserProfilesAsync()
    {
        return await _userRepository.GetAllUsersProfilesAsync();
    }
}

