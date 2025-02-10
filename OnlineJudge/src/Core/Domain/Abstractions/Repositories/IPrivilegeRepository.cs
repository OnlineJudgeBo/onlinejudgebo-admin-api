using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IPrivilegeRepository
{
    public Task CreatePrivilegeAsync(Privilege privilege);
    public Task<Privilege> GetUserPrivilegeAsync(string userId);
}
