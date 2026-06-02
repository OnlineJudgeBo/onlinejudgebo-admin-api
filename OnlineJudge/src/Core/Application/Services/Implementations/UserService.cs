using FluentValidation;
using OnlineJudgeAdmin.Core.Application.Services.Helpers;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IValidator<Problem> _userValidation;

    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IValidator<Problem> userValidator)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }

    public async Task<IEnumerable<User>> GetAllUserProfilesAsync(int siteId)
    {
        return await _userRepository.GetAllUsersProfilesAsync(siteId);
    }

    public async Task<bool> CheckUsernameAvailable(UserProfile userProfile, int siteId)
    {
        return await _userRepository.CheckUsernameAvailable(userProfile, siteId);
    }

    public async Task<bool> CheckUserEmailAvailable(UserProfile userProfile, int siteId)
    {
        return await _userRepository.CheckUserEmailAvailable(userProfile, siteId);
    }

    public async Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm, int siteId)
    {
        return await _userRepository.SearchUserProfilesAsync(searchTerm, siteId);
    }

    public async Task<UserProfile> UpdateUserProfile(User userToUpdate, string userId, int siteId)
    {
        if (userToUpdate.UserId != userId)
        {
            await _userRepository.UpdateUser(userToUpdate, userId, siteId);
        }
        return await _userRepository.UpdateUserProfile(userToUpdate.UserProfile, siteId);
    }

    public async Task ChangePassword(string password, string userId, int siteId)
    {
        string passwordEncrypt = GeneratePasswordHash(password);
        await _userRepository.ChangePassword(passwordEncrypt, userId, siteId);
    }

    public async Task DeleteRoleAsync(string userId, int roleId, int siteId)
    {
        UserRole role = await _roleRepository.GetUserRoleAsync(userId, siteId);

        if (role.Role.RoleName == "Administrador" || role.Role.RoleName == "Docente")
        {
            await _userRepository.DeleteRoleAsync(userId, roleId, siteId);
        }
        else
        {
            throw new UnauthorizedAccessException("Solo los administradores pueden eliminar roles.");
        }
    }

    public async Task DeleteUserAsync(CurrentUser currentUser, string userId, int siteId)
    {
        UserRole role = await _roleRepository.GetUserRoleAsync(userId, siteId);

        if (role == null || role.Role.RoleName == "Administrador" || role.Role.RoleName == "Docente")
        {
            await _userRepository.DeleteUserAsync(userId, siteId);
        }
        else
        {
            throw new UnauthorizedAccessException("Solo los administradores pueden eliminar usuarios.");
        }
    }

    private static string GeneratePasswordHash(string password)
    {
        return LegacyPasswordHash.Generate(password);
    }
}
