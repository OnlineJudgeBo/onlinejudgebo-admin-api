namespace OnlineJudgeAdmin.Core.Domain.Models;

// One request as the reverse proxy (Traefik) logged it.
public class AccessLogEntry
{
    public DateTime Time { get; set; }

    public string Ip { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public int Status { get; set; }

    public double DurationMs { get; set; }

    public string? UserAgent { get; set; }

    public string? Referer { get; set; }

    // Whether the current block rules reject this request.
    public bool Blocked { get; set; }
}
