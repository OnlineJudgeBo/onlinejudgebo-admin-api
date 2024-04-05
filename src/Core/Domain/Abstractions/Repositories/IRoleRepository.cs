using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IRoleRepository
{
    public Task<IEnumerable<Role>> GetNameRolesAsync();
    public Task<IEnumerable<User>> GetUserRolesAsync();
}
