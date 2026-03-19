namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseAssignment
{
    public long AssignmentId { get; set; }

    public long CourseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime OpensAt { get; set; }

    public DateTime DueAt { get; set; }

    public DateTime? LateDueAt { get; set; }

    public bool IsActive { get; set; }

    public string StatusKey { get; set; } = "upcoming";

    public string StatusLabel { get; set; } = "Proxima";

    public int ProblemCount { get; set; }

    public int AttemptsByCurrentUser { get; set; }

    public int SolvedByCurrentUser { get; set; }

    public int AcceptedByCurrentUser { get; set; }

    public List<AcademicCourseAssignmentProblem> Problems { get; set; } = new();
}

public class AcademicCourseAssignmentProblem
{
    public int ProblemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Points { get; set; }

    public bool IsVisible { get; set; }

    public bool IsSolvedByCurrentUser { get; set; }

    public int AttemptsByCurrentUser { get; set; }
}
