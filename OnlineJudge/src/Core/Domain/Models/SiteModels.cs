namespace OnlineJudgeAdmin.Core.Domain.Models;

// The name, logo, color and domains of a site, which an administrator edits to brand it.
public class SiteSettings
{
    public int SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    // Hex color such as #1e88e5.
    public string? PrimaryColor { get; set; }

    public List<string> Domains { get; set; } = [];
}

public static class SiteDomain
{
    // "Juez.Example.bo:443" and "juez.example.bo." are the same domain.
    public static string Normalize(string? host)
    {
        return (host ?? string.Empty).Trim().ToLowerInvariant().Split(':')[0].TrimEnd('.');
    }
}
