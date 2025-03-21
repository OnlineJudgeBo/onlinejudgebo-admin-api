using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class StaticsController : ControllerBase
{
    private readonly IStatiscService _staticsService;
    private readonly IMapper _mapper;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly CurrentUser _currentUser;

    public StaticsController(IStatiscService staticsService, UserClaimsHelper userClaimsHelper, IMapper mapper)
    {
        _staticsService = staticsService ?? throw new ArgumentNullException(nameof(staticsService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _currentUser = _userClaimsHelper.GetUserContextRole();
    }

    [HttpGet("GetLast365DaysSubmissionsByMonth")]
    public async Task<IActionResult> GetLast365DaysSubmissionsByMonthAsync()
    {
        return Ok(await _staticsService.GetLast365DaysSubmissionsByMonthAsync(_currentUser.SiteId));
    }

    [HttpGet("GetSubmissionsByLanguageAsync")]
    public async Task<IActionResult> GetSubmissionsByLanguageAsync()
    {
        return Ok(await _staticsService.GetSubmissionsByLanguageAsync(_currentUser.SiteId));
    }
}
