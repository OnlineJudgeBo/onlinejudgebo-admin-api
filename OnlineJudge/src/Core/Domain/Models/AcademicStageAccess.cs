namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicStageAccess
{
    public int StageId { get; set; }

    public bool IsUnlocked { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
