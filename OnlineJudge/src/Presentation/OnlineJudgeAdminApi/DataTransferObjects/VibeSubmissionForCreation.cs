namespace OnlineJudgeAdminApi.DataTransferObjects;

public class VibeSubmissionForCreation
{
    public string SourceCode { get; set; } = string.Empty;

    public int? LanguageId { get; set; }

    public string? ProblemId { get; set; }

    public int? ContestId { get; set; }

    public int? Num { get; set; }

    public string? Stdin { get; set; }

    public IReadOnlyCollection<VibeTestcaseForCreation> Testcases { get; set; } = Array.Empty<VibeTestcaseForCreation>();

    public int ProblemIdAsInt()
    {
        return int.TryParse(ProblemId, out var value) ? value : 0;
    }
}
