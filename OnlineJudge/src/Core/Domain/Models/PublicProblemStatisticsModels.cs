namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicProblemStatisticsResponse
{
    public int SiteId { get; set; }

    public int ProblemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int TotalSubmissions { get; set; }

    public int Accepted { get; set; }

    public int WrongAnswer { get; set; }

    public int TimeLimitExceeded { get; set; }

    public int MemoryLimitExceeded { get; set; }

    public int CompileError { get; set; }

    public int RuntimeError { get; set; }

    public int OtherResults { get; set; }

    public decimal AcceptanceRate { get; set; }

    public IReadOnlyCollection<ProblemStatisticsLanguageItem> Languages { get; set; } = Array.Empty<ProblemStatisticsLanguageItem>();

    public ProblemStatisticsBestRun? BestTime { get; set; }

    public ProblemStatisticsBestRun? BestMemory { get; set; }

    public IReadOnlyCollection<ProblemStatisticsAcceptedItem> FirstAccepted { get; set; } = Array.Empty<ProblemStatisticsAcceptedItem>();
}

public class ProblemStatisticsLanguageItem
{
    public int LanguageId { get; set; }

    public string LanguageName { get; set; } = string.Empty;

    public int Submissions { get; set; }

    public int Accepted { get; set; }
}

public class ProblemStatisticsBestRun
{
    public int SolutionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public string LanguageName { get; set; } = string.Empty;

    public int TimeMs { get; set; }

    public int MemoryKb { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class ProblemStatisticsAcceptedItem
{
    public int SolutionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public string LanguageName { get; set; } = string.Empty;

    public int TimeMs { get; set; }

    public int MemoryKb { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
