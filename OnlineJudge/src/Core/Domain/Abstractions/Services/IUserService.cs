using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IUserService
{
    public Task<IEnumerable<User>> GetAllUserProfilesAsync(CurrentUser currentUser, int siteId);

    public Task<bool> CheckUsernameAvailable(UserProfile userProfile, int SiteId);

    public Task<bool> CheckUserEmailAvailable(UserProfile userProfile, int SiteId);

    public Task<IEnumerable<User>> SearchUserProfilesAsync(CurrentUser currentUser, string searchTerm, int siteId);

    public Task<UserProfile> UpdateUserProfile(CurrentUser currentUser, User userToUpdate, string userId, int siteId);

    public Task ChangePassword(CurrentUser currentUser, string password, string userId, int siteId);

    public Task DeleteRoleAsync(CurrentUser currentUser, string userId, int roleId, int siteId);
    public Task DeleteUserAsync(CurrentUser currentUser, string userId, int SiteId);
}
