namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathAdminUpsertRequest
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string LanguagePrimary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Slug { get; set; }
}

public class LearningPathStageAdminRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool UnlockedByDefault { get; set; }
}

public class LearningPathTopicAdminRequest
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Theory { get; set; }
    public List<string> LearningObjectives { get; set; } = new();
    public string? Difficulty { get; set; }
    public int Order { get; set; }
    public List<int> ProblemIds { get; set; } = new();
}
