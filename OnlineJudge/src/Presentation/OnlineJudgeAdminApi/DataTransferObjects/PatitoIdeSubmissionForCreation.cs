namespace OnlineJudgeAdminApi.DataTransferObjects;

public class PatitoIdeSubmissionForCreation
{
    public string SourceCode { get; set; } = string.Empty;

    public int? LanguageId { get; set; }

    public string? ProblemId { get; set; }

    public int? ContestId { get; set; }

    public int? Num { get; set; }

    public string? Stdin { get; set; }

    public IReadOnlyCollection<PatitoIdeTestcaseForCreation> Testcases { get; set; } = Array.Empty<PatitoIdeTestcaseForCreation>();

    public int ProblemIdAsInt()
    {
        return int.TryParse(ProblemId, out var value) ? value : 0;
    }
}
