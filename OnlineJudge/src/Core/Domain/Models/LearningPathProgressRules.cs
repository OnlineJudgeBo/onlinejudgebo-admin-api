namespace OnlineJudgeAdmin.Core.Domain.Models;

public class LearningPathProgressRules
{
    public string UnlockStrategy { get; set; } = "course_controlled";

    public bool AllowFreeExploration { get; set; } = true;

    public bool CourseCanOverrideDependencies { get; set; } = true;

    public bool RecommendNextStageWhenCompleted { get; set; } = true;
}
