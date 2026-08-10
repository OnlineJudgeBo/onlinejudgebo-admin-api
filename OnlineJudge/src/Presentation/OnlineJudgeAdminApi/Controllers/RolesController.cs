using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize(Roles = AuthorizationRoles.Administrador)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IMapper _mapper;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly CurrentUser _currentUser;


    public RolesController(IRoleService roleService, UserClaimsHelper userClaimsHelper, IMapper mapper)
    {
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _currentUser = _userClaimsHelper.GetUserContextRole();
    }

    [HttpGet("")]
    public async Task<IActionResult> GetUserRolesAsync()
    {
        return Ok(await _roleService.GetUserRolesAsync(_currentUser.SiteId));
    }

    [HttpGet("rolesAvailable")]
    public async Task<IActionResult> GetAllRolesAsync()
    {
        return Ok(await _roleService.GetAllRolesAsync());
    }

    [HttpPost("{userId}/{role}")]
    public async Task<IActionResult> AddRoleToUserAsync(string userId, int role)
    {
        await _roleService.AddRoleToUserAsync(userId, role, _currentUser.SiteId);
        return Ok();
    }

    [HttpDelete("{userId}/{role}")]
    public async Task<IActionResult> RemoveRoleFromUserAsync(string userId, int role)
    {
        await _roleService.RemoveRoleFromUserAsync(userId, role, _currentUser.SiteId);
        return Ok();
    }
}
