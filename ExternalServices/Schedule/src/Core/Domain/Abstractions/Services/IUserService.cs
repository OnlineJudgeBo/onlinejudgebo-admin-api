using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IUserService
{
    public Task<IEnumerable<User>> GetAllUserProfilesAsync(int SiteId);

    public Task<bool> CheckUsernameAvailable(UserProfile userProfile, int SiteId);

    public Task<bool> CheckUserEmailAvailable(UserProfile userProfile, int SiteId);

    public Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm, int SiteId);

    public Task<UserProfile> UpdateUserProfile(User userToUpdate, string userId, int SiteId);

    public Task ChangePassword(String passwordEncrypt, string userId, int SiteId);

    public Task DeleteRoleAsync(string userId, int roleId, int SiteId);
    public Task DeleteUserAsync(CurrentUser currentUser, string userId, int SiteId);
}
