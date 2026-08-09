namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseContentItem
{
    public long ItemId { get; set; }
    public long CourseId { get; set; }
    public string Type { get; set; } = "material";
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContentUrl { get; set; }
    public string? ContentBody { get; set; }
    public long? AssignmentId { get; set; }
    public int Position { get; set; }
    public bool IsPublished { get; set; }
    public AcademicCourseAssignment? Assignment { get; set; }
}
