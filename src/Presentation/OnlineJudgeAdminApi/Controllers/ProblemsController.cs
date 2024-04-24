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
public class ProblemsController : ControllerBase
{
    private readonly IProblemService _problemService;
    private readonly IMapper _mapper;

    public ProblemsController(IProblemService problemService, IMapper mapper)
    {
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllProblemsAsync([FromQuery] string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Ok(await _problemService.GetAllProblemsAsync());
        }
        else
        {
            return Ok(await _problemService.SearchProblemAsync(searchTerm));
        }
    }

    [HttpGet("{problem_id}")]
    public async Task<IActionResult> GetProblemByIdAsync(int problem_id)
    {
        return Ok(await _problemService.GetProblemByIdAsync(problem_id));
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
        return Ok(await _problemService.CreateProblemAsync(userId, problem));
    }

    /*
        [HttpGet]
        public async Task<IActionResult> DeleteProblemByIdAsync(int problemId)
        {
            return Ok(_problemService.DeleteProblemAsync(problemId));
        }
    */
}
