namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed record IdeContextRequest(
    int? ProblemId,
    int? LanguageId,
    string? LanguageName,
    string? Handoff);
