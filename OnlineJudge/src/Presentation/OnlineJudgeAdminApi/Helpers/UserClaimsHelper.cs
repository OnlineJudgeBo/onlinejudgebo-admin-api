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

    private string? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            return null;
        }

        return GetClaimValue(user,
            ClaimTypes.NameIdentifier,
            "nameid",
            "sub",
            "user_id");
    }

    private string? GetUserRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            return null;
        }

        return GetClaimValue(user,
            ClaimTypes.Role,
            "role",
            "roles");
    }

    private int GetSiteId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var siteIdClaim = user == null
            ? null
            : GetClaimValue(user, "site_id", "siteId");

        if (int.TryParse(siteIdClaim, out var siteId))
        {
            return siteId;
        }
        return -1;
    }


    public CurrentUser GetUserContextRole()
    {
        if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authenticated user context is required.");

        var userId = GetUserId();
        var roleString = GetUserRole();
        var siteId = GetSiteId();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleString) || siteId <= 0)
            throw new UnauthorizedAccessException("The authentication token is missing required user, role or site claims.");

        return new CurrentUser
        {
            UserId = userId,
            Role = ParseRole(roleString),
            SiteId = siteId
        };
    }

    public CurrentUser? TryGetUserContextRole()
    {
        if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return GetUserContextRole();
    }

    private static string? GetClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claimValue = user.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return claimValue.Trim();
            }
        }

        return null;
    }

    private static UserRolesEnum ParseRole(string? roleString)
    {
        if (string.IsNullOrWhiteSpace(roleString))
        {
            return UserRolesEnum.Invitado;
        }

        var normalizedRole = roleString.Trim();
        if (Enum.TryParse<UserRolesEnum>(normalizedRole, ignoreCase: true, out var parsedRole))
        {
            return parsedRole;
        }

        return normalizedRole.ToLowerInvariant() switch
        {
            "admin" => UserRolesEnum.Administrador,
            "administrator" => UserRolesEnum.Administrador,
            "teacher" => UserRolesEnum.Docente,
            "docente" => UserRolesEnum.Docente,
            "assistant" => UserRolesEnum.Auxiliar,
            "auxiliar" => UserRolesEnum.Auxiliar,
            "guest" => UserRolesEnum.Invitado,
            "invitado" => UserRolesEnum.Invitado,
            _ => throw new UnauthorizedAccessException("The authentication token contains an unsupported role claim.")
        };
    }
}
