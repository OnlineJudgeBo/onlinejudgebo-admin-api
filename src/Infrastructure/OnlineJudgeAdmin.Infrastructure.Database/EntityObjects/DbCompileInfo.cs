using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;
[Table("compileinfo")]

public class DbCompileInfo
{
    [Key]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("error", TypeName = "text")]
    public required string Error { get; set; }
}
