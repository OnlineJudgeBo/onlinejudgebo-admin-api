using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("custom_input")]
public partial class DbCustomInput
{
    [Key]
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("site_id")]
    public int SiteId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual DbSolution Solution { get; set; } = null!;

    public virtual ICollection<DbCustomInputCase> Cases { get; set; } = new List<DbCustomInputCase>();
}
