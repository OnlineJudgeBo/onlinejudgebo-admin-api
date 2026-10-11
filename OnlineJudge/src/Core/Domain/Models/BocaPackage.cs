namespace OnlineJudgeAdmin.Core.Domain.Models;

// What was read from a BOCA problem package.
public record BocaPackage(
    string BaseName,
    string FullName,
    string DescriptionHtml,
    string InputHtml,
    string OutputHtml,
    string HintHtml,
    string SampleInput,
    string SampleOutput,
    int TimeLimitSeconds,
    int MemoryLimitMb,
    IReadOnlyList<string> ReviewReasons,
    IReadOnlyList<(string Input, string Output)> TestCases);

// A package uploaded for review, waiting to be confirmed by its StagingId.
public record BocaPreview(string StagingId, Problem Problem, IReadOnlyList<string> ReviewReasons, int TestCaseCount, ProblemClassificationSuggestion Suggestion);
