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
    private readonly IProblemPackageService _problemPackageService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IMapper _mapper;
    private readonly CurrentUser _currentUser;

    public ProblemsController(
        IProblemService problemService,
        IProblemPackageService problemPackageService,
        UserClaimsHelper userClaimsHelper,
        IMapper mapper)
    {
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _problemPackageService = problemPackageService ?? throw new ArgumentNullException(nameof(problemPackageService));
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
        return Ok(await _problemService.UpdateProblemAsync(userId, problemId, problem, _currentUser.SiteId));
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
        await _problemService.ChangeProblemVisibilityAsync(problemId, _currentUser.SiteId);
        return Ok();
    }

    [HttpDelete("{problemId:int}")]
    public async Task<IActionResult> DeleteProblemByIdAsync(int problemId)
    {
        await _problemService.DeleteProblemAsync(problemId, _currentUser.SiteId);
        return NoContent();
    }

    [Authorize(Roles = AuthorizationRoles.Administrador)]
    [HttpGet("{problemId:int}/export")]
    public async Task<IActionResult> ExportProblemAsync(int problemId)
    {
        try
        {
            var bytes = await _problemPackageService.ExportProblemPackageAsync(problemId, _currentUser.SiteId);
            return File(bytes, "application/zip", $"problem-{problemId}.zip");
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new ErrorDetails { StatusCode = 404, Message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = error.Message });
        }
    }

    [Authorize(Roles = AuthorizationRoles.Administrador)]
    [HttpPost("import")]
    [RequestSizeLimit(200_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
    public async Task<IActionResult> ImportProblemAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = "Sube un archivo .zip." });
        }

        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        using var stream = file.OpenReadStream();
        var problem = await _problemPackageService.ImportProblemPackageAsync(userId, stream, _currentUser.SiteId);
        return Ok(problem);
    }
}
