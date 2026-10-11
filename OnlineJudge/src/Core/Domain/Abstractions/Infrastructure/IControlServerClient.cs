using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IControlServerClient
{
    bool IsConfigured { get; }

    // Calls the control-server's admin API. Null when it cannot be reached.
    Task<ControlServerResponse?> SendAsync(string method, string pathAndQuery, string token, string? body = null, CancellationToken cancellationToken = default);
}
