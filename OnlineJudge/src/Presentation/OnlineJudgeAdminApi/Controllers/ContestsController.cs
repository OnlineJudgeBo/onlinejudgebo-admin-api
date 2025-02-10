using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class ContestsController : ControllerBase
{
    private readonly IContestService _contestService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IMapper _mapper;
    private readonly CurrentUser _currentUser;

    public ContestsController(IContestService contestService, UserClaimsHelper userClaimsHelper, IMapper mapper)
    {
        _contestService = contestService ?? throw new ArgumentNullException(nameof(contestService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = _userClaimsHelper.GetUserContextRole();
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllContestAsync()
    {

        return Ok(await _contestService.GetAllContestAsync(_currentUser));
    }

    [HttpGet("{contestId:int}")]
    public async Task<IActionResult> GetContestById(int contestId)
    {
        return Ok(await _contestService.GetContestById(contestId));
    }

    [HttpPost]
    public async Task<IActionResult> CreateContestAsync(ContestForCreation problemForCreation)
    {
        Contest problem = _mapper.Map<Contest>(problemForCreation);
        var claimsIdentity = User.Identity as ClaimsIdentity;
        var userIdClaim = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier);
        string userIdCreator = userIdClaim?.Value;
        return Ok(await _contestService.CreateContestAsync(userIdCreator, problem, problemForCreation.ManualUserList, _currentUser.SiteId));
    }

    [HttpPut("{contestId:int}")]
    public async Task<IActionResult> UpdateContestAsync(int contestId, ContestForUpdate contestForUpdate)
    {
        Contest contest = _mapper.Map<Contest>(contestForUpdate);
        contest.ContestId = contestId;
        return Ok(await _contestService.UpdateContestAsync(contestId, contest, contestForUpdate.ManualUserList, _currentUser.SiteId));
    }

    [HttpPut("{contestId:int}/promote")]
    public async Task<IActionResult> PromoteContestAsync(int contestId)
    {
        await _contestService.PromoteContestAsync(contestId, _currentUser.SiteId);
        return Ok();
    }
}
