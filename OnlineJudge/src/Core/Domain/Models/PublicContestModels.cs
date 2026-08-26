namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicContestItem
{
    public int ContestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }

    public string Status { get; set; } = "FINISHED";

    public string Track { get; set; } = "GENERAL";

    public string Level { get; set; } = "PRACTICE";

    public bool IsPrivate { get; set; }

    public bool Obi { get; set; }

    public bool IsPromoted { get; set; }

    public int DurationMinutes { get; set; }

    public int ProblemCount { get; set; }

    public int ParticipantCount { get; set; }
}

public class PublicContestsResponse
{
    public int SiteId { get; set; }

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public string FilterStatus { get; set; } = "ALL";

    public string FilterLevel { get; set; } = "ALL";

    public string SortBy { get; set; } = "DATE_DESC";

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<PublicContestItem> Items { get; set; } = Array.Empty<PublicContestItem>();
}

public class ContestReportItem
{
    public int Rank { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public string School { get; set; } = string.Empty;

    public int Solved { get; set; }

    public int Submissions { get; set; }

    public int Accepted { get; set; }

    public decimal Accuracy { get; set; }

    public bool IsVirtualParticipant { get; set; }

    public DateTime? FirstSubmitUtc { get; set; }

    public DateTime? LastSubmitUtc { get; set; }
}

public class ContestReportResponse
{
    public int ContestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }

    public string Status { get; set; } = "FINISHED";

    public string Track { get; set; } = "GENERAL";

    public string Level { get; set; } = "PRACTICE";

    public int SiteId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int DurationMinutes { get; set; }

    public bool IsPrivate { get; set; }

    public bool IsPromoted { get; set; }

    public int ProblemCount { get; set; }

    public int ParticipantCount { get; set; }

    public int TotalSubmissions { get; set; }

    public int TotalAccepted { get; set; }

    public bool IsOwner { get; set; }

    public bool CanDownloadCsv { get; set; }

    public IReadOnlyCollection<ContestReportItem> Items { get; set; } = Array.Empty<ContestReportItem>();
}
