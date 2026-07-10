namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AdminSubmissionAuditResponse
{
    public int SiteId { get; set; }

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<AdminSubmissionAuditItem> Items { get; set; } = Array.Empty<AdminSubmissionAuditItem>();
}

public class AdminSubmissionAuditItem
{
    public int SolutionId { get; set; }

    public int ProblemId { get; set; }

    public int? ContestId { get; set; }

    public string? ContestProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public string LanguageName { get; set; } = string.Empty;

    public short ResultCode { get; set; }

    public string StatusKey { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public string GeneralStatusKey { get; set; } = string.Empty;

    public string GeneralStatusLabel { get; set; } = string.Empty;

    public bool IsFinal { get; set; }

    public int TimeMs { get; set; }

    public int MemoryKb { get; set; }

    public decimal PassRate { get; set; }

    public string ClientIp { get; set; } = "0.0.0.0";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? JudgeTimeUtc { get; set; }
}
