using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("Problems")]
public class DbProblem
{
    [Key]
    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("input")]
    public string? Input { get; set; }

    [Column("output")]
    public string? Output { get; set; }

    [Column("sample_input")]
    public string? SampleInput { get; set; }

    [Column("sample_output")]
    public string? SampleOutput { get; set; }

    [Column("spj")]
    public string? Spj { get; set; }

    [Column("hint")]
    public string? Hint { get; set; }

    [Column("source")]
    public string? Source { get; set; }

    [Column("in_date")]
    public DateTime? InDate { get; set; }

    [Column("time_limit")]
    public int? TimeLimit { get; set; }

    [Column("memory_limit")]
    public int? MemoryLimit { get; set; }

    [Column("defunct")]
    public string? Defunct { get; set; }

    [Column("accepted")]
    public int? Accepted { get; set; }

    [Column("submit")]
    public int? Submit { get; set; }

    [Column("solved")]
    public int? Solved { get; set; }
    public virtual ICollection<DbProblemTag> ProblemTags { get; set; }

    public DbProblem()
    {
        ProblemTags = new HashSet<DbProblemTag>();
    }
}
