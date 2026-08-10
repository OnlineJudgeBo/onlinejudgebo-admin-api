namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseMember
{
    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public string? Lastname { get; set; }

    public string? Email { get; set; }

    public string Role { get; set; } = CourseRoleNames.Student;

    public bool IsOwner { get; set; }
}
