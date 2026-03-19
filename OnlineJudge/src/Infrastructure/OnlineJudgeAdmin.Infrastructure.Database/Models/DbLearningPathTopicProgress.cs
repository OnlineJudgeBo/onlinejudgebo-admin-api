using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("learning_path_topic_progress")]
public class DbLearningPathTopicProgress
{
    [Column("learning_path_id")]
    public long LearningPathId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("topic_id")]
    public string TopicId { get; set; } = string.Empty;

    [Column("completed_at")]
    public DateTime CompletedAt { get; set; }
}
