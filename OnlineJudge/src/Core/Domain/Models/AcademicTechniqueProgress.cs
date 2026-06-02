namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicTechniqueProgress
{
    public int TechniqueId { get; set; }

    public int SolvedCount { get; set; }

    public int RequiredCount { get; set; }

    public int MinDifficultyRequired { get; set; }

    public bool IsMastered { get; set; }
}
