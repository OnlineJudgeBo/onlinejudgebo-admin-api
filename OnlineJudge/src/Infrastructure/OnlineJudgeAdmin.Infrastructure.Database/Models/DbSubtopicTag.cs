using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("subtopic_tag")]
public class DbSubtopicTag
{
    [Column("subtopic_id")]
    public long SubtopicId { get; set; }

    [Column("tag_id")]
    public long TagId { get; set; }
}
