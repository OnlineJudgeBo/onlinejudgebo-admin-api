using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IPrivilegeRepository
{
    public Task CreatePrivilegeAsync(Privilege privilege);
}
