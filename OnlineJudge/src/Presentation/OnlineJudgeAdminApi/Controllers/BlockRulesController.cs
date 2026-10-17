using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Blocked IPs and texts, applied by BlockRuleMiddleware here and by the PHP web through "active".
[ApiController]
[Route("/api/blockrules")]
[Authorize(Roles = AuthorizationRoles.Administrador)]
public class BlockRulesController : ControllerBase
{
    private readonly IBlockRuleService _blockRuleService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public BlockRulesController(IBlockRuleService blockRuleService, IMemoryCache cache, IConfiguration configuration)
    {
        _blockRuleService = blockRuleService ?? throw new ArgumentNullException(nameof(blockRuleService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

    // For the PHP web, which has no admin session: it sends the shared BlockRules:Token instead.
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActiveAsync()
    {
        var token = _configuration["BlockRules:Token"];
        var sent = Request.Headers["X-Block-Rules-Token"].ToString();
        if (string.IsNullOrEmpty(token)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(token)))
        {
            return NotFound();
        }

        var rules = await BlockRuleCache.RulesAsync(HttpContext);
        return Ok(rules.Select(rule => new { type = rule.Type, value = rule.Value }));
    }
}
