namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicStudentProgress
{
    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public decimal TotalScore { get; set; }

    public int TotalAttempts { get; set; }

    public int TotalSolved { get; set; }

    public List<AcademicStudentAssignmentProgress> Assignments { get; set; } = new();

    public List<AcademicTechniqueProgress> Techniques { get; set; } = new();
}

public class AcademicStudentAssignmentProgress
{
    public long AssignmentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Solved { get; set; }

    public int Attempts { get; set; }

    public int Accepted { get; set; }
}
