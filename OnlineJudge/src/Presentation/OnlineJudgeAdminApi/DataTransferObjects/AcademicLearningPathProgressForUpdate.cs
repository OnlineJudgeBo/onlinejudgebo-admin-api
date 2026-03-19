namespace OnlineJudgeAdminApi.DataTransferObjects;

public class AcademicLearningPathProgressForUpdate
{
    public string? LastTopicId { get; set; }

    public List<string> CompletedTopicIds { get; set; } = new();
}
