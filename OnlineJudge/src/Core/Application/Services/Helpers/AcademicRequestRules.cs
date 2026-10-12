using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Helpers;

internal static class AcademicRequestRules
{
    public static void ValidateSite(int siteIdFromRoute, int siteIdFromToken)
    {
        if (siteIdFromRoute != siteIdFromToken)
        {
            throw new UnauthorizedAccessException("Cross-site access is forbidden.");
        }
    }

    public static void ValidateUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.Equals(userId, "defaultUserId", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid user context.");
        }
    }

    public static void ValidateRequestContext(int siteId, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        ValidateUser(currentUser.UserId);
    }
}
