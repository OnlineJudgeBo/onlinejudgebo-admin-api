using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("problem_sample_case")]
public partial class DbProblemSample
{
    [Key]
    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Key]
    [Column("num")]
    public int Num { get; set; }

    [Column("input", TypeName = "text")]
    public string? Input { get; set; }

    [Column("output", TypeName = "text")]
    public string? Output { get; set; }

    public virtual DbProblem? Problem { get; set; }
}
