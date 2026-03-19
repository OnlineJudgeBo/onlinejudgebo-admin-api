namespace OnlineJudgeAdminApi.DataTransferObjects;

public class AcademicCourseForCreation
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long? InstitutionId { get; set; }
}
