using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IIdeContextService
{
    Task<IdeContextBuildResult> BuildContextAsync(IdeLaunchClaims claims, IdeContextRequest request);
}
