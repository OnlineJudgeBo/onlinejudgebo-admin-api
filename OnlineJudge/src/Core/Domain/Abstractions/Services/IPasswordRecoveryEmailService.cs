namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IPasswordRecoveryEmailService
{
    Task SendRecoveryCodeAsync(string email, string userId, string recoveryCode, string? displayName = null);
}
