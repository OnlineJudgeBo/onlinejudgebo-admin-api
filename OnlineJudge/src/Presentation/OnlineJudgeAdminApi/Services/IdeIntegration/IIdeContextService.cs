namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public interface IIdeContextService
{
    Task<IdeContextBuildResult> BuildContextAsync(IdeLaunchClaims claims, IdeContextRequest request);
}
