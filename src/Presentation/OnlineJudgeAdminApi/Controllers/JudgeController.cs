using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class JudgeController : ControllerBase
{
    private readonly IJudgeService _judgeService;
    private readonly IMapper _mapper;

    public JudgeController(IJudgeService judgeService, IMapper mapper)
    {
        _judgeService = judgeService ?? throw new ArgumentNullException(nameof(judgeService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet("rejudge/solution/{solutionId:int}")]
    public async Task<IActionResult> RejudgeSolutionByIdAsync(int solutionId)
    {
        await _judgeService.RejudgeSolutionByIdAsync(solutionId);
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
        await _judgeService.RemoteExecutionAsync(remoteExecutionRequest, userId);
        return Ok();
    }
}
