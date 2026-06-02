namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicInstitutionRankingItem
{
    public int Rank { get; set; }

    public long InstitutionId { get; set; }

    public string InstitutionName { get; set; } = string.Empty;

    public int SolvedUnique { get; set; }
}
