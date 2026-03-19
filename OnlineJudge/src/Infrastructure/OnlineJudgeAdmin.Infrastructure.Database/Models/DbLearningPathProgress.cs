using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("learning_path_progress")]
public class DbLearningPathProgress
{
    [Column("learning_path_id")]
    public long LearningPathId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("last_topic_id")]
    public string? LastTopicId { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
