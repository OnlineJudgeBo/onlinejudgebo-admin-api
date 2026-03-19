using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("course_assignment_problem")]
public class DbCourseAssignmentProblem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("assignment_id")]
    public long AssignmentId { get; set; }

    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("points")]
    public int Points { get; set; }

    [Column("is_visible")]
    public bool IsVisible { get; set; }
}
