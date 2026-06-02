using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("learning_path_topic")]
public class DbLearningPathTopic
{
    [Column("learning_path_id")]
    public long LearningPathId { get; set; }

    [Column("topic_id")]
    public long TopicId { get; set; }
}
