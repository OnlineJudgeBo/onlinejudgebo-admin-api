namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseSummary
{
    public long CourseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string Role { get; set; } = CourseRoleNames.Student;

    public int StudentCount { get; set; }

    public int AssignmentCount { get; set; }

    public int UnlockedStages { get; set; }

    public string InviteCode { get; set; } = string.Empty;
}
