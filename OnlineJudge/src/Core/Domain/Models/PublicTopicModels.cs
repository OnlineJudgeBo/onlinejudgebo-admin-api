namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicTopicClassification
{
    public int ClassificationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ProblemCount { get; set; }
}

public class PublicTopicItem
{
    public int TopicId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ClassificationCount { get; set; }

    public int ProblemCount { get; set; }

    public IReadOnlyCollection<PublicTopicClassification> Classifications { get; set; } = Array.Empty<PublicTopicClassification>();
}

public class PublicLearningPathPhase
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Modules { get; set; } = Array.Empty<string>();

    public int MinimumProblems { get; set; }

    public string RatingRange { get; set; } = string.Empty;
}

public class PublicTopicsResponse
{
    public int SiteId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<PublicTopicItem> Topics { get; set; } = Array.Empty<PublicTopicItem>();

    public IReadOnlyCollection<PublicLearningPathPhase> LearningPath { get; set; } = Array.Empty<PublicLearningPathPhase>();

    public IReadOnlyCollection<string> TransversalSkills { get; set; } = Array.Empty<string>();
}
