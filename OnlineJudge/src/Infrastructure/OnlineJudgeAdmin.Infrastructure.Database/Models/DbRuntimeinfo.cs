using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ScheduleManager.Infrastructure.Database.Models;

[Table("runtimeinfo")]
public partial class DbRuntimeinfo
{
    [Key]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("error", TypeName = "text")]
    public string? Error { get; set; }

    public virtual DbSolution Solution { get; set; } = null!;
}
