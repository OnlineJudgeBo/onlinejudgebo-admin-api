using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IContestMachinesService
{
    public Task<ControlGroup> GetGroupAsync(int contestId, int siteId);
    // Machines that booted the ISO but are in no exam. Null when ControlServer:LobbyEnrollToken is unset.
    public ControlGroup? LobbyGroup();
    public Task<ControlGroup> GetLobbyGroupAsync(int siteId);
    public Task<LabLoginResult> LoginAsync(string username, string password, string clientIp);
}
