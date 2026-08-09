using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public class DbCourseContentItem
{
    [Column("item_id")] public long ItemId { get; set; }
    [Column("course_id")] public long CourseId { get; set; }
    [Column("item_type")] public string ItemType { get; set; } = "material";
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("content_url")] public string? ContentUrl { get; set; }
    [Column("content_body")] public string? ContentBody { get; set; }
    [Column("assignment_id")] public long? AssignmentId { get; set; }
    [Column("position")] public int Position { get; set; }
    [Column("is_published")] public bool IsPublished { get; set; }
    [Column("created_by_user_id")] public string CreatedByUserId { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
