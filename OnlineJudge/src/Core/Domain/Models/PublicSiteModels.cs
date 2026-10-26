namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicSiteResponse
{
    public int SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? PrimaryColor { get; set; }
}
