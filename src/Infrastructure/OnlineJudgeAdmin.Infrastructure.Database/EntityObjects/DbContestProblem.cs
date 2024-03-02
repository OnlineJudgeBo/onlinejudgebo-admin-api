using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("contest_problems")]
public class DbContestProblem
{
    [Key, Column("contest_id", Order = 0)]
    public int ContestId { get; set; }

    [Key, Column("problem_id", Order = 1)]
    public int ProblemId { get; set; }

    [Column("title")]
    public string Title { get; set; }

    [Column("num")]
    public int Num { get; set; }

    [ForeignKey("ContestId")]
    public virtual DbContest Contest { get; set; }

    [ForeignKey("ProblemId")]
    public virtual DbProblem Problem { get; set; }
}
