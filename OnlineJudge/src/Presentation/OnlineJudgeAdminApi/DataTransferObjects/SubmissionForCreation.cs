namespace OnlineJudgeAdminApi.DataTransferObjects;

public class SubmissionForCreation
{
    public int ProblemId { get; set; }

    public string SourceCode { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public int? ContestId { get; set; }

    public long? CourseId { get; set; }

    public long? AssignmentId { get; set; }

    public string? FileName { get; set; }
}
