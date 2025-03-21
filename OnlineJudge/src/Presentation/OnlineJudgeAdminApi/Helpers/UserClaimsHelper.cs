using System.Security.Claims;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Helpers;

public class UserClaimsHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserClaimsHelper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    }

    private string GetUserRole()
    {
        return _httpContextAccessor.HttpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
    }

    private int GetSiteId()
    {
        var siteIdClaim = _httpContextAccessor.HttpContext?.User?.Claims.FirstOrDefault(c => c.Type == "site_id")?.Value;

        if (int.TryParse(siteIdClaim, out var siteId))
        {
            return siteId;
        }
        return -1;
    }


    public CurrentUser GetUserContextRole()
    {
        var userId = GetUserId();
        var roleString = GetUserRole();

        var roleEnum = UserRolesEnum.Invitado;
        if (!string.IsNullOrEmpty(roleString) && Enum.TryParse<UserRolesEnum>(roleString, out var parsedRole))
        {
            roleEnum = parsedRole;
        }

        return new CurrentUser
        {
            UserId = userId ?? "defaultUserId",
            Role = roleEnum,
            SiteId = GetSiteId()
        };
    }
}
