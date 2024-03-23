using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IUserRepository
{
    public Task<IEnumerable<User>> GetAllUsersProfilesAsync();
}

