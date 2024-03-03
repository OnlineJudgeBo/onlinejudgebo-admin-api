using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("solution")]
public class DbSolution
{
    [Key]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; }

    [Column("time")]
    public int Time { get; set; }

    [Column("memory")]
    public int Memory { get; set; }

    [Column("in_date")]
    public DateTime InDate { get; set; }

    [Column("result")]
    public short Result { get; set; }

    [Column("language")]
    public int Language { get; set; }

    [Column("ip")]
    public string IP { get; set; }

    [Column("contest_id")]
    public int? ContestId { get; set; }

    [Column("valid")]
    public bool Valid { get; set; }

    [Column("num")]
    public int Num { get; set; }

    [Column("code_length")]
    public int CodeLength { get; set; }

    [Column("judge_time")]
    public DateTime? JudgeTime { get; set; }

    [Column("pass_rate")]
    public decimal PassRate { get; set; }

    [ForeignKey("UserId")]
    public virtual DbUser User { get; set; }
    [ForeignKey("ProblemId")]
    public virtual DbProblem Problem { get; set; }
    [ForeignKey("ContestId")]
    public virtual DbContest Contest { get; set; }
}
