using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IUserRepository
{
    public Task<User> GetUserById(string userId, int siteId);

    public Task<IEnumerable<User>> GetAllUsersProfilesAsync(int siteId);

    public Task<bool> CheckUsernameAvailable(UserProfile userProfile, int siteId);

    public Task<bool> CheckUserEmailAvailable(UserProfile userProfile, int siteId);

    public Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm, int siteId);

    public Task<User> UpdateUser(User userToUpdate, string userId, int siteId);

    public Task<UserProfile> UpdateUserProfile(UserProfile userProfileToUpdate, int siteId);

    public Task ChangePassword(string passwordEncrypt, string userId, int siteId);

    public Task DeleteRoleAsync(string userId, int roleId, int siteId);

    public Task DeleteUserAsync(string userId, int siteId);
}
