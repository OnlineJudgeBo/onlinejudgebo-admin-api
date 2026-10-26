namespace OnlineJudgeAdminApi.DataTransferObjects;

public class SiteForUpdate
{
    public string Name { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? PrimaryColor { get; set; }

    public List<string> Domains { get; set; } = [];
}
