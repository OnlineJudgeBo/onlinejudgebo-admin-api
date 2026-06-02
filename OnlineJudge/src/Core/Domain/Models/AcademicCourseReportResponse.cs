namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseReportResponse
{
    public long CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public bool CanDownloadCsv { get; set; }

    public int StudentCount { get; set; }

    public List<AcademicCourseReportAssignmentColumn> Assignments { get; set; } = new();

    public List<AcademicCourseReportItem> Items { get; set; } = new();
}

public class AcademicCourseReportAssignmentColumn
{
    public long AssignmentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int ProblemCount { get; set; }

    public DateTime OpensAt { get; set; }

    public DateTime DueAt { get; set; }
}

public class AcademicCourseReportItem
{
    public int Rank { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public int TotalSolved { get; set; }

    public int TotalAttempts { get; set; }

    public int TotalAccepted { get; set; }

    public List<AcademicCourseReportAssignmentCell> Assignments { get; set; } = new();
}

public class AcademicCourseReportAssignmentCell
{
    public long AssignmentId { get; set; }

    public int Solved { get; set; }

    public int Attempts { get; set; }

    public int Accepted { get; set; }
}
