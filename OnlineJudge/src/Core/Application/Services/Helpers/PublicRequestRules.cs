using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Helpers;

internal static class PublicRequestRules
{
    public static void ValidateSite(int siteId)
    {
        if (siteId <= 0)
        {
            throw new ArgumentException("SiteId inválido.");
        }
    }

    public static void ValidateCurrentUser(CurrentUser currentUser)
    {
        if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.UserId) || currentUser.SiteId <= 0 || currentUser.UserId == "defaultUserId")
        {
            throw new UnauthorizedAccessException("Invalid user context.");
        }
    }
}
