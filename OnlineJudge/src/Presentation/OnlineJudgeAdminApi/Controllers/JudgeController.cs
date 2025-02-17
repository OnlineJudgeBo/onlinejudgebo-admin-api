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
    public async Task<IActionResult> RejudgeSolutionByIdAsync(int id)
    {
        await _judgeService.RejudgeSolutionByIdAsync(id);
        return Ok();
    }

    [HttpGet("rejudge/problem/{problemId:int}")]
    public async Task<IActionResult> RejudgeSolutionByProblemIdAsync(int problemId)
    {
        await _judgeService.RejudgeSolutionByProblemIdAsync(problemId);
        return Ok();
    }

    [HttpPost("remoteExecutionAsync")]
    public async Task<IActionResult> RemoteExecutionAsync(RemoteExecutionForCreation remoteExecutionForCreation)
    {
        RemoteExecutionRequest remoteExecutionRequest = _mapper.Map<RemoteExecutionRequest>(remoteExecutionForCreation);

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
}
