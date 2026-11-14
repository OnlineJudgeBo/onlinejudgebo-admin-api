namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathProgressResponse
{
    public string LearningPathKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? LastTopicId { get; set; }

    public List<string> CompletedTopicIds { get; set; } = new();

    // Path problems the user has an accepted submission for on this site.
    public List<int> SolvedProblemIds { get; set; } = new();

    public DateTime? UpdatedAtUtc { get; set; }
}

public class LearningPathProgressUpdateRequest
{
    public string? LastTopicId { get; set; }

    public List<string> CompletedTopicIds { get; set; } = new();
}
