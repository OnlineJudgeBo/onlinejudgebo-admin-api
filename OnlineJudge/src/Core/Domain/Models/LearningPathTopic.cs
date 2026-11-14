namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathTopic
{
    public long TopicId { get; set; }

    public string TopicKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Theory { get; set; }

    public string? Difficulty { get; set; }

    public int SortOrder { get; set; }

    public List<string> Skills { get; set; } = new();

    public List<int> RecommendedProblems { get; set; } = new();

    // Optional extra practice; only RecommendedProblems count toward completing the topic.
    public List<int> ChallengeProblems { get; set; } = new();

    public bool HasTheory => !string.IsNullOrWhiteSpace(Theory);

    public bool HasRecommendedProblems => RecommendedProblems.Count > 0;
}
