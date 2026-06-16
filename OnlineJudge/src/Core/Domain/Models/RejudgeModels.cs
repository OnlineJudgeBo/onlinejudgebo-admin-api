namespace OnlineJudgeAdmin.Core.Domain.Models;

public class RejudgeOperationResponse
{
    public int SiteId { get; set; }

    public string Scope { get; set; } = string.Empty;

    public int Matched { get; set; }

    public DateTime RequestedAtUtc { get; set; }
}

public class RejudgeHistoryResponse
{
    public int SiteId { get; set; }

    public int Total { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<RejudgeHistoryItem> Items { get; set; } = Array.Empty<RejudgeHistoryItem>();
}

public class RejudgeHistoryItem
{
    public int SolutionId { get; set; }

    public int ProblemId { get; set; }

    public int? ContestId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
