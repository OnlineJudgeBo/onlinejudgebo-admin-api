using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("course_assignment")]
public class DbCourseAssignment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("assignment_id")]
    public long AssignmentId { get; set; }

    [Column("course_id")]
    public long CourseId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("opens_at")]
    public DateTime OpensAt { get; set; }

    [Column("due_at")]
    public DateTime DueAt { get; set; }

    [Column("late_due_at")]
    public DateTime? LateDueAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;
}
