using FluentValidation;
using Microsoft.Extensions.Configuration;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Services.Implementations;
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
        string passwordEncrypt = await GeneratePasswordHashAsync(password);
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
}
