namespace OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

public sealed class IdeSubmissionRequest
{
    public string SourceCode { get; set; } = string.Empty;

    public int? LanguageId { get; set; }

    public string? ProblemId { get; set; }

    public int? ContestId { get; set; }

    public int? Num { get; set; }

    public string? Stdin { get; set; }

    public IReadOnlyCollection<IdeTestcaseRequest> Testcases { get; set; } = Array.Empty<IdeTestcaseRequest>();

    public int ProblemIdAsInt()
    {
        return int.TryParse(ProblemId, out var value) ? value : 0;
    }
}

public sealed class IdeTestcaseRequest
{
    public string Input { get; set; } = string.Empty;

    public string? ExpectedOutput { get; set; }
}

public sealed class IdeSubmissionResponse
{
    public string SubmissionId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string StatusUrl { get; set; } = string.Empty;
}

public sealed class IdeRunResponse
{
    public string RunId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string StatusUrl { get; set; } = string.Empty;
}

public sealed class IdeSubmissionStatusResponse
{
    public string SubmissionId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Verdict { get; set; } = string.Empty;

    public string Stdout { get; set; } = string.Empty;

    public string Stderr { get; set; } = string.Empty;

    public string CompileErrors { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Logs { get; set; } = Array.Empty<string>();

    public int RuntimeMs { get; set; }

    public int MemoryKb { get; set; }
}

public sealed record IdeContextResponse(
    IdeProblemContext Problem,
    int[] AllowedLanguages,
    IdeLanguageDefinition[] LanguageDefinitions,
    IdeContextIdentifiers Identifiers);

public sealed record IdeProblemContext(
    string ProblemId,
    string Title,
    string Description,
    string Input,
    string Output,
    string Constraints,
    string? Hints,
    IdeProblemExample Example,
    string? TimeLimit,
    string? MemoryLimit);

public sealed record IdeProblemExample(string Input, string Output);

public sealed record IdeLanguageDefinition(
    int JudgeLanguageId,
    string Name,
    string? IdeLanguage);

public sealed record IdeContextIdentifiers(
    int ProblemId,
    int? ContestId,
    int? Num,
    string UserId,
    int SiteId,
    int? LanguageId,
    string? LanguageName,
    string? Handoff);

public sealed record IdeCustomInputRunCreation(
    string UserId,
    int SiteId,
    int ProblemId,
    int LanguageId,
    int? ContestId,
    int Num,
    string SourceCode,
    IReadOnlyCollection<IdeTestcaseRequest> Testcases,
    string? Stdin);
