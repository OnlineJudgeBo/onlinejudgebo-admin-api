namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IAuthorizationService
{
    public Task<bool> IsUserAdminAsync(string userId);
    public Task<bool> IsUserAssistantAsync(string userId);
    public Task<bool> IsUserTeacherAsync(string userId);
}
