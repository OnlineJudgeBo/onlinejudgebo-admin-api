namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IWelcomeEmailService
{
    Task SendWelcomeAsync(string email, string userId, string? displayName = null);
}
