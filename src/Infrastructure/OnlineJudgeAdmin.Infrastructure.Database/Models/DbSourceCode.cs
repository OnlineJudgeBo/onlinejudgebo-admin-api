using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("source_code")]
public partial class DbSourceCode
{
    [Key]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("source", TypeName = "text")]
    public string Source { get; set; } = null!;

    public virtual DbSolution Solution { get; set; } = null!;
}
