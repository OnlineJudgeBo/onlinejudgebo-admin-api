using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IUserService
{
    public Task<IEnumerable<User>> GetAllUserProfilesAsync();

    public Task<bool> CheckUsernameAvailable(UserProfile userProfile);

    public Task<bool> CheckUserEmailAvailable(UserProfile userProfile);

    public Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm);

    public Task<UserProfile> UpdateUserProfile(User userToUpdate, string userId);

    public Task ChangePassword(String passwordEncrypt, string userId);

    public Task DeleteRoleAsync(string userId, int roleId);
}
