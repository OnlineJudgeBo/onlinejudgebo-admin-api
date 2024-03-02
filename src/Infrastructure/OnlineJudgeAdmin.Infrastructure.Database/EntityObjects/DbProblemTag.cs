using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("problem_tags")]
public class DbProblemTag
{
    [Key, Column(Order = 0)]
    public int ProblemId { get; set; }

    [Key, Column(Order = 1)]
    public int TagId { get; set; }

    [ForeignKey("ProblemId")]
    public virtual required DbProblem Problem { get; set; }

    [ForeignKey("TagId")]
    public virtual required DbTag Tag { get; set; }
}
