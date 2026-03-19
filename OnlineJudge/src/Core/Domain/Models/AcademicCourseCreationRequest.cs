namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicCourseCreationRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long? InstitutionId { get; set; }
}
