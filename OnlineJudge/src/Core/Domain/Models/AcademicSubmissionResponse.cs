namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicSubmissionResponse
{
    public int SolutionId { get; set; }

    public int LanguageId { get; set; }

    public bool AutoDetected { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
