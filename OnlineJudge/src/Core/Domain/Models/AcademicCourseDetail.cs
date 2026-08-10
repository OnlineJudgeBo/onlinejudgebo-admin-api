namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseDetail
{
    public long CourseId { get; set; }

    public int SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long? InstitutionId { get; set; }

    public string LearningPathKey { get; set; } = string.Empty;

    public string LearningPathTitle { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public string? MemberRole { get; set; }

    public int StudentCount { get; set; }

    public int AssignmentCount { get; set; }

    public bool CanManage { get; set; }

    public List<AcademicCourseAssignment> Assignments { get; set; } = new();

    public List<AcademicCourseContentItem> Content { get; set; } = new();

    public List<AcademicStageAccess> Stages { get; set; } = new();
}
