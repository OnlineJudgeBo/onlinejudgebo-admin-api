using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IRoleRepository
{
    public Task<IEnumerable<Role>> GetAllRolesAsync();
    public Task<IEnumerable<User>> GetUserRolesAsync();
    public Task AddRoleToUserAsync(string user, int role);
    public Task RemoveRoleFromUserAsync(string userId, int roleId);
    public Task<UserRole> GetUserRoleAsync(string userId);
}
