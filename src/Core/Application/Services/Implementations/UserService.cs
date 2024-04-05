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

    public async Task<bool> CheckUsernameAvailable(UserProfile userProfile)
    {
        return await _userRepository.CheckUsernameAvailable(userProfile);
    }

    public async Task<bool> CheckUserEmailAvailable(UserProfile userProfile)
    {
        return await _userRepository.CheckUserEmailAvailable(userProfile);
    }

    public async Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm)
    {
        return await _userRepository.SearchUserProfilesAsync(searchTerm);
    }
}
