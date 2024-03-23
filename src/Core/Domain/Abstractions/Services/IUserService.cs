using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IUserService
{
    public Task<IEnumerable<User>> GetAllUserProfilesAsync();
}

