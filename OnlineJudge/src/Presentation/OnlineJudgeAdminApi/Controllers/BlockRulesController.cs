using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Blocked IPs and texts, applied by BlockRuleMiddleware here and by the PHP web, which reads the table.
[ApiController]
[Route("/api/blockrules")]
[Authorize(Roles = AuthorizationRoles.Administrador)]
public class BlockRulesController : ControllerBase
{
    private readonly IBlockRuleService _blockRuleService;
    private readonly IMemoryCache _cache;

    public BlockRulesController(IBlockRuleService blockRuleService, IMemoryCache cache)
    {
        _blockRuleService = blockRuleService ?? throw new ArgumentNullException(nameof(blockRuleService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
    {
        return Ok(await _blockRuleService.GetAllAsync());
    }

    [HttpPost]
    public async Task<IActionResult> AddAsync(BlockRuleForCreation request)
    {
        var rule = await _blockRuleService.AddAsync(request.Type, request.Value, request.Note);
        _cache.Remove(BlockRuleCache.CacheKey);
        return Ok(rule);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        await _blockRuleService.DeleteAsync(id);
        _cache.Remove(BlockRuleCache.CacheKey);
        return Ok();
    }

    // The latest requests seen by Traefik, to find what to block.
    [HttpGet("requests")]
    public async Task<IActionResult> GetRecentRequestsAsync(string? ip, string? text, int limit = 200)
    {
        return Ok(await _blockRuleService.GetRecentRequestsAsync(ip, text, limit));
    }
}
