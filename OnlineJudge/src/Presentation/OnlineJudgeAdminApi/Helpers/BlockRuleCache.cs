using Microsoft.Extensions.Caching.Memory;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Helpers;

// Block rules cached for every request; the matching itself is BlockRule.Blocks.
public static class BlockRuleCache
{
    public const string CacheKey = "block-rules";
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(30);

    public static async Task<IReadOnlyList<BlockRule>> RulesAsync(HttpContext httpContext)
    {
        var cache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var service = httpContext.RequestServices.GetRequiredService<IBlockRuleService>();
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTime;
            return (IReadOnlyList<BlockRule>)(await service.GetAllAsync()).ToList();
        }) ?? [];
    }
}
