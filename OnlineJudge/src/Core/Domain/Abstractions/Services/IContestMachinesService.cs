using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestMachinesService
{
    public Task<ControlGroup> GetGroupAsync(int contestId, int siteId);
    public Task<LabLoginResult> LoginAsync(string username, string password, string clientIp);
}
