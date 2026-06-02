namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathTrack
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Version { get; set; }

    public List<string> TargetAudience { get; set; } = new();

    public int EstimatedTotalProblems { get; set; }
}
