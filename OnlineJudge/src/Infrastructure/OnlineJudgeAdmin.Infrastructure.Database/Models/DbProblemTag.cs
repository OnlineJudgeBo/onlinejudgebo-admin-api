using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("problem_tag")]
public class DbProblemTag
{
    [Key]
    [Column("tag_id")]
    public long TagId { get; set; }

    [Column("tag_key")]
    public string TagKey { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }
}
