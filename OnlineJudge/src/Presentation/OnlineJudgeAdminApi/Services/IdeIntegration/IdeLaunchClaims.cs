namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed record IdeLaunchClaims(
    string UserId,
    int SiteId,
    int ProblemId,
    int? ContestId,
    int? Num,
    int[] AllowedLanguages);
