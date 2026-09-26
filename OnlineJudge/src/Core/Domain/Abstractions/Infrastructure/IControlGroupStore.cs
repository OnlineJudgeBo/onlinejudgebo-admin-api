using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

// Registers a group in the control-server's groups.json so its machines can enroll.
public interface IControlGroupStore
{
    Task EnsureAsync(ControlGroup group);
}
