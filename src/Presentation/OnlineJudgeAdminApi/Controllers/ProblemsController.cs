using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
//[Authorize]
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
    public async Task<IActionResult> GetAllProblemsAsync()
    {
        return Ok(await _problemService.GetAllProblemsAsync());
    }

    [HttpGet("{problem_id}")]
    public async Task<IActionResult> GetProblemByIdAsync(int problem_id)
    {
        return Ok(await _problemService.GetProblemByIdAsync(problem_id));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProblemAsync(ProblemForCreation problemForCreation)
    {
        Problem problem = _mapper.Map<Problem>(problemForCreation);

        return Ok(await _problemService.CreateProblemAsync(problem));
    }
    /*
        [HttpGet]
        public async Task<IActionResult> EditProblemAsync(int problemId, Problem problem)
        {
            return Ok(_problemService.EditProblemAsync(problemId, problem));
        }

        [HttpGet]
        public async Task<IActionResult> DeleteProblemByIdAsync(int problemId)
        {
            return Ok(_problemService.DeleteProblemAsync(problemId));
        }
    */
}
