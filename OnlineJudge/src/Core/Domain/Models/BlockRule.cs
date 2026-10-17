using System.Net;

namespace OnlineJudgeAdmin.Core.Domain.Models;

// A request is rejected when its client IP matches an "ip" rule (address or CIDR) or when its
// User-Agent, Referer or URL contains a "text" rule, ignoring case. Same matching as
// patito-client-web public/BlockRules.php: keep both in step.
public class BlockRule
{
    public const string IpType = "ip";
    public const string TextType = "text";

    public int Id { get; set; }

    public string Type { get; set; } = IpType;

    public string Value { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public static bool Blocks(IEnumerable<BlockRule> rules, string ip, string text)
    {
        IPAddress.TryParse(ip, out var address);
        return rules.Any(rule => rule.Type == IpType
            ? rule.Value == ip || (address != null && IPNetwork.TryParse(rule.Value, out var network) && network.Contains(address))
            : text.Contains(rule.Value, StringComparison.OrdinalIgnoreCase));
    }

    // What "text" rules look at: User-Agent, Referer and the requested URL.
    public static string RequestText(string? userAgent, string? referer, string? url)
    {
        return $"{userAgent} {referer} {url}";
    }
}
