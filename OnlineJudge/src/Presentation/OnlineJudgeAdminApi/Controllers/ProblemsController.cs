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
[Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
public class ProblemsController : ControllerBase
{
    private readonly IProblemService _problemService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IMapper _mapper;
    private readonly CurrentUser _currentUser;

    public ProblemsController(IProblemService problemService, UserClaimsHelper userClaimsHelper, IMapper mapper)
    {
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = _userClaimsHelper.GetUserContextRole();
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllProblemsAsync([FromQuery] string? searchTerm)
    {
        CurrentUser currentUser = _userClaimsHelper.GetUserContextRole();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Ok(await _problemService.GetAllProblemsAsync(currentUser));
        }
        else
        {
            return Ok(await _problemService.SearchProblemAsync(currentUser, searchTerm));
        }
    }

    [HttpGet("{problem_id}")]
    public async Task<IActionResult> GetProblemByIdAsync(int problem_id)
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _problemService.GetProblemByIdAsync(problem_id, currentUser.SiteId));
    }

    [HttpPut("{problemId:int}")]
    public async Task<IActionResult> UpdateProblemAsync(int problemId, ProblemForUpdate problemForUpdate)
    {
        Problem problem = _mapper.Map<Problem>(problemForUpdate);
        problem.ProblemId = problemId;
        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userIdClaim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);
        string userId = userIdClaim?.Value;
        return Ok(await _problemService.UpdateProblemAsync(userId, problemId, problem));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProblemAsync(ProblemForCreation problemForCreation)
    {
        Problem problem = _mapper.Map<Problem>(problemForCreation);
        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userIdClaim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);
        string userId = userIdClaim?.Value;
        return Ok(await _problemService.CreateProblemAsync(userId, problem, _currentUser.SiteId));
    }

    [HttpPut("{problemId:int}/visibility")]
    public async Task<IActionResult> ChangeProblemVisibilityAsync(int problemId)
    {
        await _problemService.ChangeProblemVisibilityAsync(problemId);
        return Ok();
    }

    [HttpDelete("{problemId:int}")]
    public async Task<IActionResult> DeleteProblemByIdAsync(int problemId)
    {
        await _problemService.DeleteProblemAsync(problemId, _currentUser.SiteId);
        return NoContent();
    }
}
