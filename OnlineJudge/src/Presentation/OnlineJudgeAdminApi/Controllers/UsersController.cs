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
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly CurrentUser _currentUser;


    public UsersController(IUserService userService, UserClaimsHelper userClaimsHelper, IMapper mapper)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = _userClaimsHelper.GetUserContextRole();
    }

    [HttpGet()]
    [Authorize(Roles = AuthorizationRoles.AdministradorDocenteAuxiliar)]
    public async Task<IActionResult> GetAllUserProfilesAsync([FromQuery] string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Ok(await _userService.GetAllUserProfilesAsync(_currentUser, _currentUser.SiteId));
        }
        else
        {
            string termToSearch = searchTerm.Trim();
            return Ok(await _userService.SearchUserProfilesAsync(_currentUser, termToSearch, _currentUser.SiteId));
        }
    }

    [HttpPost("UsernameIsAvailable")]
    public async Task<ActionResult<bool>> CheckUsernameAvailable(UserAvailableForRequest userForValidation)
    {
        UserProfile userProfile = _mapper.Map<UserProfile>(userForValidation);

        return Ok(await _userService.CheckUsernameAvailable(userProfile, _currentUser.SiteId));
    }

    [HttpPost("UserEmailIsAvailable")]
    public async Task<ActionResult<bool>> CheckUserEmailAvailable(UserAvailableForRequest userForValidation)
    {
        UserProfile userProfile = _mapper.Map<UserProfile>(userForValidation);

        return Ok(await _userService.CheckUserEmailAvailable(userProfile, _currentUser.SiteId));
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult> UpdateProfileUser(UserForUpdate userToUpdate, string userId)
    {
        User userProfile = _mapper.Map<User>(userToUpdate);
        return Ok(await _userService.UpdateUserProfile(_currentUser, userProfile, userId, _currentUser.SiteId));
    }

    [HttpPut("changePassword/{userId}")]
    public async Task<ActionResult> ChangePassword(UserPasswordForUpdate newPassword, string userId)
    {
        await _userService.ChangePassword(_currentUser, newPassword.Password, userId, _currentUser.SiteId);
        return Ok();
    }

    [HttpDelete("{userId}/role/{roleId:int}")]
    public async Task<ActionResult> DeleteRole(string userId, int roleId)
    {
        await _userService.DeleteRoleAsync(_currentUser, userId, roleId, _currentUser.SiteId);
        return Ok();
    }

    [HttpDelete("{userId}")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        CurrentUser currentUser = _userClaimsHelper.GetUserContextRole();
        await _userService.DeleteUserAsync(currentUser, userId, _currentUser.SiteId);
        return Ok();
    }
}
