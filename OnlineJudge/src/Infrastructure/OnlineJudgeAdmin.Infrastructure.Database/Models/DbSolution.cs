using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("solution")]
public partial class DbSolution
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("time")]
    public int Time { get; set; }

    [Column("memory")]
    public int Memory { get; set; }

    [Column("in_date")]
    public DateTime InDate { get; set; }

    [Column("result")]
    public short Result { get; set; }

    [Column("language")]
    public uint Language { get; set; }

    [Column("ip")]
    public string Ip { get; set; } = null!;

    [Column("contest_id")]
    public int? ContestId { get; set; }

    [Column("num")]
    public int Num { get; set; }

    [Column("code_length")]
    public int CodeLength { get; set; }

    [Column("judgetime")]
    public DateTime? Judgetime { get; set; }

    [Column("pass_rate")]
    public decimal PassRate { get; set; }

    [Column("is_remote_oj")]
    public bool IsRemoteOj { get; set; }

    [Column("remote_id")]
    public int? RemoteId { get; set; }

    [Column("site_id")]
    public int SiteId { get; set; }

    public virtual DbCompileinfo? Compileinfo { get; set; }

    public virtual DbProblem Problem { get; set; } = null!;

    public virtual DbRuntimeinfo? Runtimeinfo { get; set; }

    public virtual DbSourceCode? SourceCode { get; set; }

    public virtual DbUser User { get; set; } = null!;
}
