using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("course_submission_context")]
public class DbCourseSubmissionContext
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("solution_id")]
    public long SolutionId { get; set; }

    [Column("course_id")]
    public long CourseId { get; set; }

    [Column("assignment_id")]
    public long AssignmentId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
