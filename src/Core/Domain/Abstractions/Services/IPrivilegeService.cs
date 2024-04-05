using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IPrivilegeService
{
    public Task<IEnumerable<Privilege>> SavePrivilegeAsync();
}
