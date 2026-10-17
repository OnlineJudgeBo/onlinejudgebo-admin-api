using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

// The latest requests of the reverse proxy's access log, newest first.
public interface IAccessLogReader
{
    Task<IReadOnlyList<AccessLogEntry>> ReadLastAsync(int max);
}
