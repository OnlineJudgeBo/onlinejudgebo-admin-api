using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class ProgrammingLanguagesController : ControllerBase
{
    private readonly IProgrammingLanguageService _programmingLanguageService;
    private readonly IMapper _mapper;

    public ProgrammingLanguagesController(IProgrammingLanguageService programmingLanguageService, IMapper mapper)
    {
        _programmingLanguageService = programmingLanguageService ?? throw new ArgumentNullException(nameof(programmingLanguageService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllContestAsync()
    {
        return Ok(await _programmingLanguageService.GetAllProgrammingLanguageAsync());
    }
}
