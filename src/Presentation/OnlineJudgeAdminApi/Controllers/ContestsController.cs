using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
//[Authorize]
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
}
