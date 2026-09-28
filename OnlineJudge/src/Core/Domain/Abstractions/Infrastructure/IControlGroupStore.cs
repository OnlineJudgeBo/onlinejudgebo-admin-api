using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

// Registers a group through control-server so its machines can enroll.
public interface IControlGroupStore
{
    Task EnsureAsync(ControlGroup group);
}
