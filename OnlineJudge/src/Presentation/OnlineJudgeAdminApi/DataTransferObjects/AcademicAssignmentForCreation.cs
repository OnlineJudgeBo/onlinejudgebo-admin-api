namespace OnlineJudgeAdminApi.DataTransferObjects;

public class AcademicAssignmentForCreation
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? OpensAt { get; set; }

    public DateTime? DueAt { get; set; }

    public DateTime? LateDueAt { get; set; }

    public bool IsActive { get; set; } = true;

    public List<int> ProblemIds { get; set; } = new();
}
