using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public class AcademicCourseMemberForCreation
{
    public string UserId { get; set; } = string.Empty;

    public string Role { get; set; } = CourseRoleNames.Student;
}
