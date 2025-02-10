using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IRoleService
{
    public Task<IEnumerable<Role>> GetAllRolesAsync();
    public Task<IEnumerable<User>> GetUserRolesAsync(int siteId);
    public Task AddRoleToUserAsync(string userId, int roleId, int siteId);
    public Task RemoveRoleFromUserAsync(string userId, int role, int siteId);
}
