namespace OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

public sealed record IdeContextRequest(
    int? ProblemId,
    int? LanguageId,
    string? LanguageName,
    string? Handoff);
