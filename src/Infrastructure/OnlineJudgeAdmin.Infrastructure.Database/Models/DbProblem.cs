using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("problem")]
public partial class DbProblem
{
    [Key]
    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    [Column("input", TypeName = "text")]
    public string? Input { get; set; }

    [Column("output", TypeName = "text")]
    public string? Output { get; set; }

    [Column("sample_input", TypeName = "text")]
    public string? SampleInput { get; set; }

    [Column("sample_output", TypeName = "text")]
    public string? SampleOutput { get; set; }

    [Column("spj")]
    public string Spj { get; set; } = null!;

    [Column("hint", TypeName = "text")]
    public string? Hint { get; set; }

    [Column("source")]
    public string? Source { get; set; }

    [Column("in_date")]
    public DateTime? InDate { get; set; } = DateTime.MinValue;

    [Column("time_limit")]
    public int TimeLimit { get; set; }

    [Column("memory_limit")]
    public int MemoryLimit { get; set; }

    [Column("defunct")]
    public string Defunct { get; set; } = null!;

    [Column("accepted")]
    public int? Accepted { get; set; }

    [Column("submit")]
    public int? Submit { get; set; }

    [Column("solved")]
    public int? Solved { get; set; }

    public virtual ICollection<DbContestProblem>? ContestProblems { get; set; }
    public virtual ICollection<DbSolution>? Solutions { get; set; }
    public virtual ICollection<DbClassification>? Classifications { get; set; }
}
