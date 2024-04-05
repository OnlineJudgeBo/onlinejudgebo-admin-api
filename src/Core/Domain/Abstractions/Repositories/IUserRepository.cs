using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IUserRepository
{
    public Task<IEnumerable<User>> GetAllUsersProfilesAsync();

    public Task<bool> CheckUsernameAvailable(UserProfile userProfile);

    public Task<bool> CheckUserEmailAvailable(UserProfile userProfile);

    public Task<IEnumerable<User>> SearchUserProfilesAsync(string searchTerm);
}
