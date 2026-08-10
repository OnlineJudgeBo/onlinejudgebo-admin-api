namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathTrack
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Version { get; set; }

    public string LanguagePrimary { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public List<string> TargetAudience { get; set; } = new();

    public int EstimatedTotalProblems { get; set; }
}
