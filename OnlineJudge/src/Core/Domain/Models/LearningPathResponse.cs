namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathResponse
{
    public LearningPathTrack Track { get; set; } = new();

    public List<LearningPathStage> Stages { get; set; } = new();

    public LearningPathProgressRules ProgressRules { get; set; } = new();
}
