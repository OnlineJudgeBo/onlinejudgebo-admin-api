using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("course")]
public class DbCourse
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("course_id")]
    public long CourseId { get; set; }

    [Column("course_key")]
    public string CourseKey { get; set; } = string.Empty;

    [Column("invite_code")]
    public string InviteCode { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }
}
