using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class StaticsController : ControllerBase
{
    private readonly IStatiscService _staticsService;
    private readonly IMapper _mapper;
    public StaticsController(IStatiscService staticsService, IMapper mapper)
    {
        _staticsService = staticsService ?? throw new ArgumentNullException(nameof(staticsService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet("GetLast365DaysSubmissionsByMonth")]
    public async Task<IActionResult> GetLast365DaysSubmissionsByMonthAsync()
    {
        return Ok(await _staticsService.GetLast365DaysSubmissionsByMonthAsync());
    }

    [HttpGet("GetSubmissionsByLanguageAsync")]
    public async Task<IActionResult> GetSubmissionsByLanguageAsync()
    {
        return Ok(await _staticsService.GetSubmissionsByLanguageAsync());
    }
}
