using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IBlockRuleService
{
    Task<IEnumerable<BlockRule>> GetAllAsync();

    Task<BlockRule> AddAsync(string type, string value, string? note);

    Task DeleteAsync(int id);

    // The latest requests, optionally only those whose IP starts with ip or that contain text.
    Task<IReadOnlyList<AccessLogEntry>> GetRecentRequestsAsync(string? ip, string? text, int limit);
}
