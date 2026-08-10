using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class JudgeController : ControllerBase
{
    private readonly IJudgeService _judgeService;
    private readonly ISolutionService _solutionService;
    private readonly IMapper _mapper;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly CurrentUser _currentUser;

    public JudgeController(IJudgeService judgeService, UserClaimsHelper userClaimsHelper, ISolutionService solutionService, IMapper mapper)
    {
        _judgeService = judgeService ?? throw new ArgumentNullException(nameof(judgeService));
        _solutionService = solutionService ?? throw new ArgumentNullException(nameof(solutionService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _currentUser = _userClaimsHelper.GetUserContextRole();
    }

    [HttpGet("rejudge/solution/{id:int}")]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> RejudgeSolutionByIdAsync(int id)
    {
        return Ok(await _judgeService.RejudgeSolutionByIdAsync(_currentUser.SiteId, id));
    }

    [HttpPatch("solution/{id:int}/verdict")]
    public async Task<IActionResult> ManuallyJudgeSolutionAsync(
        int id,
        [FromBody] ManualJudgeForUpdate request)
    {
        if (!HasManualJudgeRole(User))
        {
            return Forbid();
        }

        return Ok(await _judgeService.ManuallyJudgeSolutionAsync(
            _currentUser.SiteId,
            id,
            request.ResultCode));
    }

    [HttpGet("rejudge/problem/{problemId:int}")]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> RejudgeSolutionByProblemIdAsync(int problemId)
    {
        return Ok(await _judgeService.RejudgeSolutionByProblemIdAsync(_currentUser.SiteId, problemId));
    }

    [HttpGet("rejudge/contest/{contestId:int}")]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> RejudgeSolutionByContestIdAsync(int contestId)
    {
        return Ok(await _judgeService.RejudgeSolutionByContestIdAsync(_currentUser.SiteId, contestId));
    }

    [HttpGet("rejudge/range")]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> RejudgeSolutionsByRangeAsync(
        [FromQuery] int fromSolutionId,
        [FromQuery] int toSolutionId)
    {
        return Ok(await _judgeService.RejudgeSolutionsByRangeAsync(_currentUser.SiteId, fromSolutionId, toSolutionId));
    }

    [HttpGet("rejudge/language/{languageId:int}")]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> RejudgeSolutionsByLanguageAsync(int languageId)
    {
        return Ok(await _judgeService.RejudgeSolutionsByLanguageAsync(_currentUser.SiteId, languageId));
    }

    [HttpGet("rejudge/history")]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> GetRejudgeHistoryAsync([FromQuery] int limit = 50)
    {
        return Ok(await _judgeService.GetRejudgeHistoryAsync(_currentUser.SiteId, limit));
    }

    [HttpPost("remoteExecutionAsync")]
    public async Task<IActionResult> RemoteExecutionAsync(RemoteExecutionForCreation remoteExecutionForCreation)
    {
        RemoteExecutionRequest remoteExecutionRequest = _mapper.Map<RemoteExecutionRequest>(remoteExecutionForCreation);
        remoteExecutionRequest.ClientIp = ClientIpHelper.GetClientIp(HttpContext);

        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userIdClaim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);
        string userId = userIdClaim?.Value;
        await _judgeService.RemoteExecutionAsync(remoteExecutionRequest, userId, _currentUser.SiteId);
        return Ok();
    }

    [HttpPost("remoteExecutionResult")]
    public async Task<IActionResult> RemoteExecutionResult(RemoteExecutionResult remoteResult)
    {
        Solution solution = _mapper.Map<Solution>(remoteResult);
        await _solutionService.UpdateSolutionRemoteAsync(solution);
        return Ok();
    }

    private static bool HasManualJudgeRole(ClaimsPrincipal user)
    {
        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Administrador",
            "Docente",
            "Auxiliar"
        };

        return user.Claims
            .Where(claim => claim.Type is ClaimTypes.Role or "role" or "roles")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(allowedRoles.Contains);
    }
}
