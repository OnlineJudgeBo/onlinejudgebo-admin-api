namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseMemberCreationRequest
{
    public string UserId { get; set; } = string.Empty;

    public string Role { get; set; } = CourseRoleNames.Student;
}
