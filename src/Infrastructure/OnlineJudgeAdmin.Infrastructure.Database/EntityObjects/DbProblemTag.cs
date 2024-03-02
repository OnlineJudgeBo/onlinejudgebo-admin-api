using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("problem_tags")]
public class DbProblemTag
{
    [Key]
    [Column("problem_id", Order = 0)]
    public int ProblemId { get; set; }

    [Key]
    [Column("tag_id", Order = 1)]
    public int TagId { get; set; }

    [ForeignKey(nameof(ProblemId))]
    public virtual DbProblem? Problem { get; set; }

    [ForeignKey(nameof(TagId))]
    public virtual DbTag? Tag { get; set; }
}
