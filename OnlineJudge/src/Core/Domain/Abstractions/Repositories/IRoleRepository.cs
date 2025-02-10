using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IRoleRepository
{
    public Task<IEnumerable<Role>> GetAllRolesAsync();
    public Task<IEnumerable<User>> GetUserRolesAsync(int siteId);
    public Task AddRoleToUserAsync(string user, int role, int siteId);
    public Task RemoveRoleFromUserAsync(string userId, int roleId, int SiteId);
    public Task<UserRole> GetUserRoleAsync(string userId, int siteId);
}
