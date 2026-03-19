namespace OnlineJudgeAdmin.Core.Domain.Models;

public class DashboardMetricSummary
{
    public int Problems { get; set; }

    public int ActiveUsers { get; set; }

    public int SubmissionsLast30Days { get; set; }

    public int ActiveContests { get; set; }
}

public class UpcomingContest
{
    public int ContestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }
}

public class PublicDashboardResponse
{
    public int SiteId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public DashboardMetricSummary Metrics { get; set; } = new();

    public IReadOnlyCollection<string> TrendingTopics { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<UpcomingContest> UpcomingContests { get; set; } = Array.Empty<UpcomingContest>();
}
