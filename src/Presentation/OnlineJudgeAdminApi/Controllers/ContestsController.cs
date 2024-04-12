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
public class ContestsController : ControllerBase
{
    private readonly IContestService _contestService;
    private readonly IMapper _mapper;

    public ContestsController(IContestService contestService, IMapper mapper)
    {
        _contestService = contestService ?? throw new ArgumentNullException(nameof(contestService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllContestAsync()
    {
        return Ok(await _contestService.GetAllContestAsync());
    }

    [HttpGet("{contestId:int}")]
    public async Task<IActionResult> GetContestById(int contestId)
    {
        return Ok(await _contestService.GetContestById(contestId));

        /*Contest problem = _mapper.Map<Contest>(problemForCreation);
        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userIdClaim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);
        string userId = userIdClaim?.Value;
        return Ok(await _contestService.CreateContestAsync(userId, problem));*/
    }

    [HttpPost]
    public async Task<IActionResult> CreateContestAsync(ContestForCreation problemForCreation)
    {

        Contest problem = _mapper.Map<Contest>(problemForCreation);
        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userIdClaim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);
        string userId = userIdClaim?.Value;
        return Ok(await _contestService.CreateContestAsync(userId, problem));
    }
}
