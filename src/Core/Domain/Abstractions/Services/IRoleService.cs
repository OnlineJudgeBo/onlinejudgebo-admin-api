using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IRoleService
{
    public Task<IEnumerable<Role>> GetNameRolesAsync();
    public Task<IEnumerable<User>> GetUserRolesAsync();
}
