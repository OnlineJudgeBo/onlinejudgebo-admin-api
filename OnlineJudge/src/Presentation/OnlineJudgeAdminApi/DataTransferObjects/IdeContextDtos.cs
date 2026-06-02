namespace OnlineJudgeAdminApi.DataTransferObjects;

public sealed record IdeContextResponse(
    IdeProblemContextDto Problem,
    int[] AllowedLanguages,
    IdeLanguageDefinitionDto[] LanguageDefinitions,
    IdeContextIdentifiersDto Identifiers);

public sealed record IdeProblemContextDto(
    string ProblemId,
    string Title,
    string Description,
    string Input,
    string Output,
    string Constraints,
    string? Hints,
    IdeProblemExampleDto Example,
    string? TimeLimit,
    string? MemoryLimit);

public sealed record IdeProblemExampleDto(string Input, string Output);

public sealed record IdeLanguageDefinitionDto(
    int JudgeLanguageId,
    string Name,
    string? IdeLanguage);

public sealed record IdeContextIdentifiersDto(
    int ProblemId,
    int? ContestId,
    int? Num,
    string UserId,
    int SiteId,
    int? LanguageId,
    string? LanguageName,
    string? Handoff);
