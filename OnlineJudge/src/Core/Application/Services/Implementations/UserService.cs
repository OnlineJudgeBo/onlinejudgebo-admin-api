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

    public async Task<IEnumerable<User>> GetAllUserProfilesAsync(CurrentUser currentUser, int siteId)
    {
        EnsureUserManager(currentUser, siteId);
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

    public async Task<IEnumerable<User>> SearchUserProfilesAsync(CurrentUser currentUser, string searchTerm, int siteId)
    {
        EnsureUserManager(currentUser, siteId);
        return await _userRepository.SearchUserProfilesAsync(searchTerm, siteId);
    }

    public async Task<UserProfile> UpdateUserProfile(CurrentUser currentUser, User userToUpdate, string userId, int siteId)
    {
        EnsureSameSite(currentUser, siteId);
        if (!string.Equals(currentUser.UserId, userId, StringComparison.Ordinal)
            && currentUser.Role is not UserRolesEnum.Administrador and not UserRolesEnum.Auxiliar)
            throw new UnauthorizedAccessException("Solo el propio usuario, un administrador o un auxiliar pueden actualizar el perfil.");

        if (userToUpdate.UserId != userId)
        {
            await _userRepository.UpdateUser(userToUpdate, userId, siteId);
        }
        return await _userRepository.UpdateUserProfile(userToUpdate.UserProfile, siteId);
    }

    public async Task ChangePassword(CurrentUser currentUser, string password, string userId, int siteId)
    {
        EnsureSameSite(currentUser, siteId);
        if (!string.Equals(currentUser.UserId, userId, StringComparison.Ordinal)
            && currentUser.Role is not UserRolesEnum.Administrador and not UserRolesEnum.Auxiliar)
        {
            throw new UnauthorizedAccessException("Solo el propio usuario, un administrador o un auxiliar pueden cambiar la contraseña.");
        }

        string passwordEncrypt = GeneratePasswordHash(password);
        await _userRepository.ChangePassword(passwordEncrypt, userId, siteId);
    }

    public async Task DeleteRoleAsync(CurrentUser currentUser, string userId, int roleId, int siteId)
    {
        EnsureSameSite(currentUser, siteId);
        if (currentUser.Role != UserRolesEnum.Administrador)
        {
            throw new UnauthorizedAccessException("Solo los administradores pueden eliminar roles.");
        }

        await _userRepository.DeleteRoleAsync(userId, roleId, siteId);
    }

    public async Task DeleteUserAsync(CurrentUser currentUser, string userId, int siteId)
    {
        EnsureSameSite(currentUser, siteId);
        if (currentUser.Role != UserRolesEnum.Administrador)
        {
            throw new UnauthorizedAccessException("Solo los administradores pueden eliminar usuarios.");
        }

        await _userRepository.DeleteUserAsync(userId, siteId);
    }

    private static void EnsureSameSite(CurrentUser currentUser, int siteId)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        if (siteId <= 0 || currentUser.SiteId != siteId)
            throw new UnauthorizedAccessException("El usuario no pertenece al sitio solicitado.");
    }

    private static void EnsureUserManager(CurrentUser currentUser, int siteId)
    {
        EnsureSameSite(currentUser, siteId);
        if (currentUser.Role is not UserRolesEnum.Administrador and not UserRolesEnum.Auxiliar and not UserRolesEnum.Docente)
            throw new UnauthorizedAccessException("Only administrators, assistants, or teachers can view users.");
    }

    private static string GeneratePasswordHash(string password)
    {
        return LegacyPasswordHash.Generate(password);
    }
}
