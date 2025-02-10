using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("contest_problem")]
public partial class DbContestProblem
{
    [Key]
    [Column("contest_id")]
    public int? ContestId { get; set; }

    [Key]
    [Column("problem_id")]
    public int? ProblemId { get; set; }

    [Column("title")]
    public string? Title { get; set; } = null!;

    [Column("num")]
    public int? Num { get; set; }

    public virtual DbContest? Contest { get; set; } = null!;

    public virtual DbProblem? Problem { get; set; } = null!;
}
