using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Middleware;

// Rejects blocked clients with an empty 403 before anything else runs.
public class BlockRuleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BlockRuleMiddleware> _logger;

    public BlockRuleMiddleware(RequestDelegate next, ILogger<BlockRuleMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        // The rules screen stays reachable so an administrator who blocked themselves can undo it.
        if (!httpContext.Request.Path.StartsWithSegments("/api/blockrules") && await IsBlockedAsync(httpContext))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(httpContext);
    }

    private async Task<bool> IsBlockedAsync(HttpContext httpContext)
    {
        try
        {
            var request = httpContext.Request;
            var text = BlockRule.RequestText(request.Headers.UserAgent, request.Headers.Referer, $"{request.Path}{request.QueryString}");
            return BlockRule.Blocks(await BlockRuleCache.RulesAsync(httpContext), ClientIpHelper.GetClientIp(httpContext), text);
        }
        catch (Exception ex)
        {
            // Without the rules the API keeps serving rather than going down with the table.
            _logger.LogError(ex, "No se pudieron leer las reglas de bloqueo");
            return false;
        }
    }
}
