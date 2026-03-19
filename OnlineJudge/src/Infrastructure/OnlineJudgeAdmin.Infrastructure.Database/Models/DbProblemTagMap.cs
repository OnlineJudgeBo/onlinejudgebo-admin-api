using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("problem_tag_map")]
public class DbProblemTagMap
{
    [Column("problem_id")]
    public long ProblemId { get; set; }

    [Column("tag_id")]
    public long TagId { get; set; }
}
