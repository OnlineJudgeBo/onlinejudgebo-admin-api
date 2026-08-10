using System.ComponentModel.DataAnnotations.Schema;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("course_user")]
public class DbCourseUser
{
    [Column("course_id")]
    public long CourseId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = CourseRoleNames.Student;
}
