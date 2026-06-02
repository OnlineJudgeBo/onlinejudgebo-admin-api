using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("learning_path")]
public class DbLearningPath
{
    [Key]
    [Column("learning_path_id")]
    public long LearningPathId { get; set; }

    [Column("learning_path_key")]
    public string LearningPathKey { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("version")]
    public int Version { get; set; }

    [Column("language_primary")]
    public string LanguagePrimary { get; set; } = string.Empty;

    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("slug")]
    public string? Slug { get; set; }
}
