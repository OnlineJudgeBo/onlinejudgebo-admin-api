using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IIdeLaunchTokenIssuer
{
    string Issue(IdeLaunchClaims claims);
}
