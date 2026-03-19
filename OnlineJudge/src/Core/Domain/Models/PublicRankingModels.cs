namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicRankingItem
{
    public int Rank { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public string School { get; set; } = string.Empty;

    public int Solved { get; set; }

    public int Submit { get; set; }

    public decimal Ratio { get; set; }
}

public class PublicRankingResponse
{
    public int SiteId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<PublicRankingItem> Items { get; set; } = Array.Empty<PublicRankingItem>();
}
