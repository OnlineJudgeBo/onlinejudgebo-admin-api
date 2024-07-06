using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IUserRepository
{
    public Task<User> GetUserById(string userId);

    public Task<IEnumerable<User>> GetAllUsersProfilesAsync();

    public Task<bool> CheckUsernameAvailable(UserProfile userProfile);

    public Task<bool> CheckUserEmailAvailable(UserProfile userProfile);

    public Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm);

    public Task<User> UpdateUser(User userToUpdate, string userId);

    public Task<UserProfile> UpdateUserProfile(UserProfile userProfileToUpdate);

    public Task ChangePassword(string passwordEncrypt, string userId);

    public Task DeleteRoleAsync(string userId, int roleId);
    public Task DeleteUserAsync(string userId);
}
