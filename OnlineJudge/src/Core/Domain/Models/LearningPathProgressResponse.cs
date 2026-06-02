namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathProgressResponse
{
    public string LearningPathKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? LastTopicId { get; set; }

    public List<string> CompletedTopicIds { get; set; } = new();

    public DateTime? UpdatedAtUtc { get; set; }
}

public class LearningPathProgressUpdateRequest
{
    public string? LastTopicId { get; set; }

    public List<string> CompletedTopicIds { get; set; } = new();
}
