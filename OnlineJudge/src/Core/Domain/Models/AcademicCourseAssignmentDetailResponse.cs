namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseAssignmentDetailResponse
{
    public long CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string? CourseDescription { get; set; }

    public string? MemberRole { get; set; }

    public bool CanManage { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int StudentCount { get; set; }

    public int ParticipantCount { get; set; }

    public int TotalSubmissions { get; set; }

    public int TotalAccepted { get; set; }

    public AcademicCourseAssignment Assignment { get; set; } = new();

    public List<AcademicCourseAssignmentRankingItem> Items { get; set; } = new();
}

public class AcademicCourseAssignmentRankingItem
{
    public int Rank { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public int Solved { get; set; }

    public int Submissions { get; set; }

    public int Accepted { get; set; }

    public decimal Accuracy { get; set; }
}
