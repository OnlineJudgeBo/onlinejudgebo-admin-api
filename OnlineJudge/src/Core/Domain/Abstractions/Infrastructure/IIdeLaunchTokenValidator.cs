using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IIdeLaunchTokenValidator
{
    IdeLaunchClaims Validate(string? token);
}
