using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;
[Table("contest_problems")]
public class DbContestProblem
{
    [Key, Column(Order = 0)]
    public int ContestId { get; set; }
    [Key, Column(Order = 1)]
    public int ProblemId { get; set; }
    public string Title { get; set; }
    public int Num { get; set; }

    [ForeignKey("ContestId")]
    public virtual DbContest Contest { get; set; }
    [ForeignKey("ProblemId")]
    public virtual DbProblem Problem { get; set; }
}
