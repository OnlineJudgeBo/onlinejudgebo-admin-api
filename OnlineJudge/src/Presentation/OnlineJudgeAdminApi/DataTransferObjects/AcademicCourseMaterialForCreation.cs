namespace OnlineJudgeAdminApi.DataTransferObjects;

public class AcademicCourseMaterialForCreation
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContentUrl { get; set; }
    public string? ContentBody { get; set; }
    public bool IsPublished { get; set; } = true;
}
