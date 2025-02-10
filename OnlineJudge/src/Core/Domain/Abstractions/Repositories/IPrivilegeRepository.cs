using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IPrivilegeRepository
{
    public Task CreatePrivilegeAsync(Privilege privilege);
    public Task<Privilege> GetUserPrivilegeAsync(string userId);
}
