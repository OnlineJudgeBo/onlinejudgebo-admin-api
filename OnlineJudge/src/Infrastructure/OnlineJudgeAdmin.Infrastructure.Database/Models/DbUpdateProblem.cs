using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("problem")]
public partial class DbUpdateProblem
{
    [Column("title")]
    public string Title { get; set; }

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

    [Column("origin_source")]
    public string? OriginSource { get; set; }

    [Column("time_limit")]
    public int TimeLimit { get; set; }

    [Column("memory_limit")]
    public int MemoryLimit { get; set; }

    [Column("defunct")]
    public string Defunct { get; set; }

    public virtual ICollection<DbContestProblem>? ContestProblems { get; set; }

    public virtual ICollection<DbSolution>? Solutions { get; set; }

    public virtual ICollection<DbClassification>? Classifications { get; set; }
}
