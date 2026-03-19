using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("subtopic")]
public class DbSubtopic
{
    [Key]
    [Column("subtopic_id")]
    public long SubtopicId { get; set; }

    [Column("topic_id")]
    public long TopicId { get; set; }

    [Column("subtopic_key")]
    public string SubtopicKey { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("theory")]
    public string? Theory { get; set; }

    [Column("learning_objectives")]
    public string? LearningObjectives { get; set; }

    [Column("difficulty_band")]
    public string? DifficultyBand { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }
}
