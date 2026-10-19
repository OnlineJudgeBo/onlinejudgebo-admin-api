namespace OnlineJudgeAdminApi.DataTransferObjects;

public class ClarificationAnswerRequest
{
    public string Answer { get; set; } = string.Empty;

    // true: everyone in the contest sees the question and the answer, without who asked.
    public bool IsPublic { get; set; }
}

public class ClarificationAnnouncementRequest
{
    public int? ProblemNum { get; set; }

    public string Text { get; set; } = string.Empty;
}

public class ClarificationQuestionRequest
{
    public int? ProblemNum { get; set; }

    public string Question { get; set; } = string.Empty;
}
