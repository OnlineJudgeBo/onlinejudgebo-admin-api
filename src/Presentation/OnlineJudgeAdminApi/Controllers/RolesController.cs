using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IMapper _mapper;

    public RolesController(IRoleService roleService, IMapper mapper)
    {
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet("")]
    public async Task<IActionResult> GetUserRolesAsync()
    {
        return Ok(await _roleService.GetUserRolesAsync());
    }

    [HttpGet("rolesAvailable")]
    public async Task<IActionResult> GetNameRolesAsync()
    {
        return Ok(await _roleService.GetNameRolesAsync());
    }
}

