using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("compileinfo")]
public partial class DbCompileinfo
{
    [Key]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("error", TypeName = "text")]
    public string? Error { get; set; }

    public virtual DbSolution Solution { get; set; } = null!;
}
