namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathTopic
{
    public long TopicId { get; set; }

    public string TopicKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Theory { get; set; }

    public List<string> Skills { get; set; } = new();

    public List<int> RecommendedProblems { get; set; } = new();

    public bool HasTheory => !string.IsNullOrWhiteSpace(Theory);

    public bool HasRecommendedProblems => RecommendedProblems.Count > 0;
}
