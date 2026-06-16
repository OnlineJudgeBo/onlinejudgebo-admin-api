namespace OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

public sealed record IdeLaunchClaims(
    string UserId,
    int SiteId,
    int ProblemId,
    int? ContestId,
    int? Num,
    int[] AllowedLanguages);
