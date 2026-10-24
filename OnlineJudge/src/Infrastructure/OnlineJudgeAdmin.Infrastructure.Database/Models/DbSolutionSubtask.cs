using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("solution_subtask")]
[PrimaryKey(nameof(SolutionId), nameof(Subtask))]
public partial class DbSolutionSubtask
{
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("subtask")]
    public int Subtask { get; set; }

    [Column("points", TypeName = "decimal(10,4)")]
    public decimal Points { get; set; }

    [Column("max_points", TypeName = "decimal(10,4)")]
    public decimal MaxPoints { get; set; }

    [Column("tests")]
    public int Tests { get; set; }
}
