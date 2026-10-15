using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// Login service the lab ISO calls (AUTH_SERVICE_URL). Answers the same contract as the
// control-server's auth-server.py, but with Patito accounts: the region is the exam group.
[ApiController]
[Route("/api/lab")]
[AllowAnonymous]
public partial class LabLoginController : ControllerBase
{
    private const int MaxFailedLogins = 10;
    private static readonly TimeSpan FailedLoginWindow = TimeSpan.FromMinutes(5);
    private static readonly object FailedLoginLock = new();
    private readonly IContestMachinesService _machinesService;
    private readonly IMachinesPanelService _panelService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public LabLoginController(IContestMachinesService machinesService, IMachinesPanelService panelService, IConfiguration configuration, IMemoryCache cache)
    {
        _machinesService = machinesService ?? throw new ArgumentNullException(nameof(machinesService));
        _panelService = panelService ?? throw new ArgumentNullException(nameof(panelService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LabLoginRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var clientIp = ClientIpHelper.GetClientIp(HttpContext);
        var failureKey = $"lab-login:{clientIp}:{username.ToLowerInvariant()}";
        lock (FailedLoginLock)
        {
            if (_cache.TryGetValue<int>(failureKey, out var failures) && failures >= MaxFailedLogins)
            {
                return Ok(new { ok = false, message = "Demasiados intentos. Espera unos minutos." });
            }
        }

        var result = await _machinesService.LoginAsync(username, request.Password ?? string.Empty, clientIp);
        lock (FailedLoginLock)
        {
            if (result.Authenticated)
            {
                _cache.Remove(failureKey);
            }
            else
            {
                var failures = _cache.TryGetValue<int>(failureKey, out var count) ? count : 0;
                _cache.Set(failureKey, failures + 1, FailedLoginWindow);
            }
        }

        // Valid credentials without an active exam still grant desktop access,
        // but no contest group/token is returned to the ISO.
        if (result.Authenticated && result.Group == null)
        {
            var guestBaseUrl = (_configuration["Base:Url"] ?? string.Empty).TrimEnd('/');
            // The machine stays in the lobby, so it opens the homepage set in the lobby panel.
            var lobbyHomepage = _machinesService.LobbyGroup() is { } lobby
                ? await _panelService.GetGroupSettingAsync(lobby, "homepage", "url", onlyIfSet: true)
                : null;
            return Ok(new
            {
                ok = true,
                authenticated = true,
                hasActiveExam = false,
                userId = result.UserId,
                displayName = result.DisplayName,
                homepage = lobbyHomepage ?? (guestBaseUrl.Length > 0 ? $"{guestBaseUrl}/oj/index.php" : string.Empty),
                team = new { id = TeamId(result.UserId), name = result.DisplayName },
                message = result.Message,
            });
        }

        // Invalid credentials are the only failed login outcome.
        if (!result.Ok || result.Group == null)
        {
            return Ok(new { ok = false, message = result.Message });
        }

        var baseUrl = (_configuration["Base:Url"] ?? string.Empty).TrimEnd('/');
        var homepage = await _panelService.GetGroupSettingAsync(result.Group, "homepage", "url", onlyIfSet: true)
            ?? (baseUrl.Length > 0 ? $"{baseUrl}/oj/contest.php?cid={result.ContestId}" : string.Empty);
        var logoUrl = await _panelService.GetGroupSettingAsync(result.Group, "logo", "effective_url") ?? _configuration["ControlServer:LogoUrl"] ?? string.Empty;
        // The ISO prints the team name in big letters under the logo on the desktop background.
        var teamName = $"{result.Group.Label} · {result.DisplayName}";

        return Ok(new
        {
            ok = true,
            authenticated = true,
            hasActiveExam = true,
            userId = result.UserId,
            displayName = result.DisplayName,
            homepage,
            logoUrl,
            team = new { id = TeamId(result.UserId), name = teamName.Length > 128 ? teamName[..128] : teamName },
            region = new { id = result.Group.Id, name = result.Group.Label, enrollToken = result.Group.EnrollToken },
        });
    }

    // The ISO only accepts team ids made of [A-Za-z0-9._-] without "..".
    private static string TeamId(string userId)
    {
        var id = UnsafeTeamIdChars().Replace(userId, "-").Replace("..", "-");
        return id.Length == 0 ? "equipo" : id[..Math.Min(id.Length, 64)];
    }

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeTeamIdChars();
}
