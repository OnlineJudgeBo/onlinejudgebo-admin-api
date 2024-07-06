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
    public async Task<IActionResult> GetAllRolesAsync()
    {
        return Ok(await _roleService.GetAllRolesAsync());
    }

    [HttpPost("{userId}/{role}")]
    public async Task<IActionResult> AddRoleToUserAsync(string userId, int role)
    {
        await _roleService.AddRoleToUserAsync(userId, role);
        return Ok();
    }

    [HttpDelete("{userId}/{role}")]
    public async Task<IActionResult> RemoveRoleFromUserAsync(string userId, int role)
    {
        await _roleService.RemoveRoleFromUserAsync(userId, role);
        return Ok();
    }
}

