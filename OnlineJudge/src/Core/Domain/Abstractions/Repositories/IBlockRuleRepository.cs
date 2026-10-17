using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IBlockRuleRepository
{
    Task<IEnumerable<BlockRule>> GetAllAsync();

    Task<BlockRule> AddAsync(BlockRule rule);

    Task DeleteAsync(int id);
}
