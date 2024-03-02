using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;
[Table("solutions")]

public class DbSolution
{

    [Key]
    public int SolutionId { get; set; }
    public int ProblemId { get; set; }
    public string UserId { get; set; }
    public int Time { get; set; }
    public int Memory { get; set; }
    public DateTime InDate { get; set; }
    public short Result { get; set; }
    public int Language { get; set; }
    public string IP { get; set; }
    public int? ContestId { get; set; }
    public bool Valid { get; set; }
    public int Num { get; set; }
    public int CodeLength { get; set; }
    public DateTime? JudgeTime { get; set; }
    public decimal PassRate { get; set; }

    [ForeignKey("UserId")]
    public virtual DbUser User { get; set; }
    [ForeignKey("ProblemId")]
    public virtual DbProblem Problem { get; set; }
    [ForeignKey("ContestId")]
    public virtual DbContest Contest { get; set; }
}
