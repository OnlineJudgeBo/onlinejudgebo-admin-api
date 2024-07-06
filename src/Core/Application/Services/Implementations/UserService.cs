using FluentValidation;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IValidator<Problem> _userValidation;
    private readonly IConfiguration _configuration;

    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IValidator<Problem> userValidator,
        IConfiguration configuration)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

    public async Task<UserProfile> UpdateUserProfile(User userToUpdate, string userId)
    {
        if (userToUpdate.UserId != userId)
        {
            await _userRepository.UpdateUser(userToUpdate, userId);
        }
        return await _userRepository.UpdateUserProfile(userToUpdate.UserProfile);
    }

    public async Task ChangePassword(string password, string userId)
    {
        string passwordEncrypt = await GeneratePasswordHashAsync(password);
        await _userRepository.ChangePassword(passwordEncrypt, userId);
    }

    public async Task DeleteRoleAsync(string userId, int roleId)
    {
        UserRole role = await _roleRepository.GetUserRoleAsync(userId);

        if (role.Role.RoleName == "Administrador" || role.Role.RoleName == "Docente" )
        {
            await _userRepository.DeleteRoleAsync(userId, roleId);
        }
        else
        {
            throw new UnauthorizedAccessException("Solo los administradores pueden eliminar roles.");
        }
    }

    private async Task<string> GeneratePasswordHashAsync(string password)
    {
        using (HttpClient client = new HttpClient())
        {
            string baseUrl = _configuration.GetSection("Base:Url").Value;
            string endpoint = $"/spi.php?spi={Uri.EscapeDataString(password)}";
            client.BaseAddress = new Uri(baseUrl);

            try
            {
                HttpResponseMessage response = await client.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
                return $"ERROR: {e.Message}";
            }
        }
    }

    public async Task DeleteUserAsync(CurrentUser currentUser, string userId)
    {
        UserRole role = await _roleRepository.GetUserRoleAsync(userId);

        if ( role == null || role.Role.RoleName == "Administrador" || role.Role.RoleName == "Docente" )
        {
            await _userRepository.DeleteUserAsync(userId);
        }
        else
        {
            throw new UnauthorizedAccessException("Solo los administradores pueden eliminar usuarios.");
        }
    }
}
