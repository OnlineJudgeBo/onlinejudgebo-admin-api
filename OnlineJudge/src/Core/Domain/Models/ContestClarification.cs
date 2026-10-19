namespace OnlineJudgeAdmin.Core.Domain.Models;

// A participant's question with the judge's answer, or a judge announcement (no UserId, no Question).
// Public ones are shown to everyone in the contest; the rest only to whoever asked.
public class ContestClarification
{
    public const int MaxTextLength = 2000;

    public int Id { get; set; }

    public int ContestId { get; set; }

    public string? UserId { get; set; }

    // Contest problem number (0 = A); null = general.
    public int? ProblemNum { get; set; }

    public string? Question { get; set; }

    public string? Answer { get; set; }

    public bool IsPublic { get; set; }

    public string? AnsweredBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? AnsweredAt { get; set; }
}

public class ContestClarificationPendingCount
{
    public int ContestId { get; set; }

    public int Pending { get; set; }
}
