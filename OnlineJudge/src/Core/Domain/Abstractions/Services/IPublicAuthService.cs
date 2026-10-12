using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IPublicAuthService
{
    Task<PublicAuthenticatedUser> LoginAsync(string userOrEmail, string password, int siteId, string clientIp = "0.0.0.0");

    Task<PublicAuthenticatedUser> RegisterAsync(string userId, string password, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress);

    Task RequestPasswordRecoveryAsync(string email, int siteId);

    Task ResetPasswordWithRecoveryCodeAsync(string email, string recoveryCode, int siteId);

    Task<PublicAuthenticatedUser> GetAuthenticatedUserAsync(CurrentUser currentUser);
}
