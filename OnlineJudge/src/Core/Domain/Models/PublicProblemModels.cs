namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicProblemItem
{
    public int ProblemId { get; set; }

    public int? ContestId { get; set; }

    public string? ContestProblemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Accepted { get; set; }

    public int Submit { get; set; }

    public decimal SuccessRate { get; set; }

    public string Difficulty { get; set; } = "HARD";

    public bool IsSolvedByCurrentUser { get; set; }

    public bool IsAttemptedByCurrentUser { get; set; }

    public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<int> Years { get; set; } = Array.Empty<int>();

    public IReadOnlyCollection<string> ContestTracks { get; set; } = Array.Empty<string>();

    public string OriginSource { get; set; } = string.Empty;
}

public class PublicProblemsResponse
{
    public int SiteId { get; set; }

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public IReadOnlyCollection<PublicProblemItem> Items { get; set; } = Array.Empty<PublicProblemItem>();
}

public class ProblemSampleCase
{
    public int Index { get; set; }

    public string Input { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;
}

public class PublicProblemDetailResponse
{
    public int ProblemId { get; set; }

    public int? ContestId { get; set; }

    public string? ContestProblemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InputSpec { get; set; } = string.Empty;

    public string OutputSpec { get; set; } = string.Empty;

    public string Hint { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTime? PublishedAtUtc { get; set; }

    public decimal TimeLimitSeconds { get; set; }

    public int MemoryLimitMb { get; set; }

    public int Score { get; set; }

    public int Accepted { get; set; }

    public int Submit { get; set; }

    public int Solved { get; set; }

    public decimal SuccessRate { get; set; }

    public string Difficulty { get; set; } = "HARD";

    public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<int> Years { get; set; } = Array.Empty<int>();

    public IReadOnlyCollection<string> ContestTracks { get; set; } = Array.Empty<string>();

    public string OriginSource { get; set; } = string.Empty;

    public IReadOnlyCollection<ProblemSampleCase> SampleCases { get; set; } = Array.Empty<ProblemSampleCase>();
}

public class PublicProblemFiltersResponse
{
    public int SiteId { get; set; }

    public IReadOnlyCollection<int> Years { get; set; } = Array.Empty<int>();

    public IReadOnlyCollection<string> ContestTracks { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> ContestSources { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<PublicProblemMenuItem> ProblemMenuItems { get; set; } = Array.Empty<PublicProblemMenuItem>();

    public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();
}

public class PublicProblemMenuItem
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ContestTrack { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
