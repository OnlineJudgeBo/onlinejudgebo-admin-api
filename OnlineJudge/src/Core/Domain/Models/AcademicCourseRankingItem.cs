namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseRankingItem
{
    public int Rank { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public decimal TotalScore { get; set; }

    public int Solved { get; set; }

    public int Attempts { get; set; }

    public List<AcademicCourseRankingAssignmentSummary> Assignments { get; set; } = new();
}

public class AcademicCourseRankingAssignmentSummary
{
    public long AssignmentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int ProblemCount { get; set; }

    public int Solved { get; set; }
}
