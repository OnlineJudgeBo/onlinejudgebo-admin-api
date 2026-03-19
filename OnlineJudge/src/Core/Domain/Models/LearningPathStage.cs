namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathStage
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public string Difficulty { get; set; } = "basic";

    public string Description { get; set; } = string.Empty;

    public List<string> LearningObjectives { get; set; } = new();

    public bool UnlockedByDefault { get; set; }

    public List<int> Dependencies { get; set; } = new();

    public List<LearningPathTopic> Topics { get; set; } = new();
}
