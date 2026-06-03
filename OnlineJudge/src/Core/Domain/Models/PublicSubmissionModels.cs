namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicSubmissionRequest
{
    public int? ProblemId { get; set; }

    public string? ContestProblemId { get; set; }

    public string SourceCode { get; set; } = string.Empty;

    public int? LanguageId { get; set; }

    public int? ContestId { get; set; }

    public int? Num { get; set; }

    public long? CourseId { get; set; }

    public long? AssignmentId { get; set; }

    public string? FileName { get; set; }
}

public class PublicSubmissionResponse
{
    public int SolutionId { get; set; }

    public int LanguageId { get; set; }

    public bool AutoDetected { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class PublicSubmissionStatusResponse
{
    public int SolutionId { get; set; }

    public int ProblemId { get; set; }

    public int? ContestId { get; set; }

    public string? ContestProblemId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public short ResultCode { get; set; }

    public string StatusKey { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public string GeneralStatusKey { get; set; } = string.Empty;

    public string GeneralStatusLabel { get; set; } = string.Empty;

    public bool IsFinal { get; set; }

    public int TimeMs { get; set; }

    public int MemoryKb { get; set; }

    public decimal PassRate { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? JudgeTimeUtc { get; set; }

    public string? CompileMessage { get; set; }

    public string? RuntimeMessage { get; set; }

    public string? SourceCode { get; set; }
}

public class PublicSubmissionListItem
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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? JudgeTimeUtc { get; set; }
}

public class PublicSubmissionsResponse
{
    public int SiteId { get; set; }

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<PublicSubmissionListItem> Items { get; set; } = Array.Empty<PublicSubmissionListItem>();
}

public class PublicSubmissionSourceCodeItem
{
    public int SolutionId { get; set; }

    public int ProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public string LanguageName { get; set; } = string.Empty;

    public string StatusKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string SourceCode { get; set; } = string.Empty;
}
