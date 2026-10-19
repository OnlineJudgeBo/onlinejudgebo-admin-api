using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("contest_clarification")]
public partial class DbContestClarification
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("contest_id")]
    public int ContestId { get; set; }

    [Column("site_id")]
    public int SiteId { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("problem_num")]
    public int? ProblemNum { get; set; }

    [Column("question", TypeName = "text")]
    public string? Question { get; set; }

    [Column("answer", TypeName = "text")]
    public string? Answer { get; set; }

    [Column("is_public")]
    public bool IsPublic { get; set; }

    [Column("answered_by")]
    public string? AnsweredBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("answered_at")]
    public DateTime? AnsweredAt { get; set; }
}
