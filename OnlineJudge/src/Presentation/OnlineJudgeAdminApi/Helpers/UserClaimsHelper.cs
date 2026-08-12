using System.Security.Claims;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Helpers;

public class UserClaimsHelper
{
    // Higher-privilege roles
    private static readonly UserRolesEnum[] RolePriorityOrder =
    {
        UserRolesEnum.Administrador,
        UserRolesEnum.Docente,
        UserRolesEnum.Auxiliar,
        UserRolesEnum.Invitado
    };

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
        var siteId = GetSiteId();
        var role = GetHighestPriorityRole();
        if (string.IsNullOrWhiteSpace(userId) || role == null || siteId <= 0)
            throw new UnauthorizedAccessException("The authentication token is missing required user, role or site claims.");

        return new CurrentUser
        {
            UserId = userId,
            Role = role.Value,
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

    /// <summary>
    /// Collects every role found across the role-ish claim types (a token may carry
    /// several roles as either separate claims or one comma-separated claim value,
    /// e.g. "Administrador,Docente") and returns the highest-privilege one.
    /// </summary>
    private UserRolesEnum? GetHighestPriorityRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            return null;
        }

        var roles = new HashSet<UserRolesEnum>();
        foreach (var claimType in new[] { ClaimTypes.Role, "role", "roles" })
        {
            foreach (var claim in user.Claims.Where(c => c.Type == claimType))
            {
                foreach (var value in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var role = TryParseRole(value);
                    if (role.HasValue)
                    {
                        roles.Add(role.Value);
                    }
                }
            }
        }

        if (roles.Count == 0)
        {
            return null;
        }

        foreach (var role in RolePriorityOrder)
        {
            if (roles.Contains(role))
            {
                return role;
            }
        }

        return roles.First();
    }

    private static UserRolesEnum? TryParseRole(string roleString)
    {
        var normalizedRole = roleString.Trim();
        if (normalizedRole.Length == 0)
        {
            return null;
        }

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
            _ => null
        };
    }
}
