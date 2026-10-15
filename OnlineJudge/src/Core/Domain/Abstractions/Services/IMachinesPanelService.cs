using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IMachinesPanelService
{
    // Forwards a call of the machines panel to the control-server, limited to the exam's group.
    Task<MachinesForwardResult> ForwardAsync(int contestId, int siteId, string method, string? path, string query, string? body, CancellationToken cancellationToken = default);

    // The same, limited to the lobby: machines that booted the ISO but are in no exam.
    Task<MachinesForwardResult> ForwardLobbyAsync(int siteId, string method, string? path, string query, string? body, CancellationToken cancellationToken = default);

    // Homepage and logo set for the exam in the machines panel; null when unset or the control-server is down.
    Task<string?> GetGroupSettingAsync(ControlGroup group, string setting, string field, bool onlyIfSet = false);
}
