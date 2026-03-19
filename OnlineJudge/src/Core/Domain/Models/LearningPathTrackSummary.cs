namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathTrackSummary
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Version { get; set; }

    public string LanguagePrimary { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<string> TargetAudience { get; set; } = new();

    public int StageCount { get; set; }

    public int EstimatedTotalProblems { get; set; }
}
