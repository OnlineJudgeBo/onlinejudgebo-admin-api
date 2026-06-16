namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicOnlineUsersResponse
{
    public int SiteId { get; set; }

    public int WindowMinutes { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<PublicOnlineUserItem> Items { get; set; } = Array.Empty<PublicOnlineUserItem>();
}

public class PublicOnlineUserItem
{
    public string Hash { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;

    public string? Referer { get; set; }

    public string? Uri { get; set; }

    public DateTime? FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }
}
