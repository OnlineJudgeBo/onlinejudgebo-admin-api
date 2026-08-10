namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathStage
{
    public long StageId { get; set; }

    public string StageKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string Difficulty { get; set; } = "basic";

    public string Description { get; set; } = string.Empty;

    public List<string> LearningObjectives { get; set; } = new();

    public bool UnlockedByDefault { get; set; }

    public List<long> Dependencies { get; set; } = new();

    public List<LearningPathTopic> Topics { get; set; } = new();
}
