using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface IPrivilegeService
{
    public Task<IEnumerable<Privilege>> SavePrivilegeAsync();
}
