using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("topic")]
public class DbAcademicTopic
{
    [Key]
    [Column("topic_id")]
    public long TopicId { get; set; }

    [Column("topic_key")]
    public string TopicKey { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("unlocked_by_default")]
    public bool UnlockedByDefault { get; set; }
}
