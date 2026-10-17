using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IBlockRuleService
{
    Task<IEnumerable<BlockRule>> GetAllAsync();

    Task<BlockRule> AddAsync(string type, string value, string? note);

    Task DeleteAsync(int id);
}
