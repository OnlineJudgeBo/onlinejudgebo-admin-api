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
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public UsersController(IUserService userService, IMapper mapper)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllUserProfilesAsync([FromQuery] string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Ok(await _userService.GetAllUserProfilesAsync());
        }
        else
        {
                string termToSearch = searchTerm.Trim();
            return Ok(await _userService.SearchUserProfilesAsync(termToSearch));
        }
    }

    [HttpPost("UsernameIsAvailable")]
    public async Task<ActionResult<bool>> CheckUsernameAvailable(UserAvailableForRequest userForValidation)
    {
        UserProfile userProfile = _mapper.Map<UserProfile>(userForValidation);

        return Ok(await _userService.CheckUsernameAvailable(userProfile));
    }

    [HttpPost("UserEmailIsAvailable")]
    public async Task<ActionResult<bool>> CheckUserEmailAvailable(UserAvailableForRequest userForValidation)
    {
        UserProfile userProfile = _mapper.Map<UserProfile>(userForValidation);

        return Ok(await _userService.CheckUserEmailAvailable(userProfile));
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult> UpdateProfileUser(UserForUpdate userToUpdate, string userId)
    {
        User userProfile = _mapper.Map<User>(userToUpdate);
        return Ok(await _userService.UpdateUserProfile(userProfile, userId));
    }

    [HttpPut("changePassword/{userId}")]
    public async Task<ActionResult> ChangePassword(UserPasswordForUpdate newPassword, string userId)
    {
        await _userService.ChangePassword(newPassword.Password, userId);
        return Ok();
    }

    [HttpDelete("{userId}/role/{roleId:int}")]
    public async Task<ActionResult> DeleteRole(string userId, int roleId)
    {
        await _userService.DeleteRoleAsync(userId, roleId);
        return Ok();
    }
}
