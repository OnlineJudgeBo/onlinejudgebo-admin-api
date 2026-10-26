namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicHealthResponse
{
    // "ok", or "judge_stuck" when submissions wait too long.
    public string Status { get; set; } = "ok";

    public int StuckSubmissions { get; set; }
}

public class PublicSiteResponse
{
    public int SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? PrimaryColor { get; set; }
}
