namespace OnlineJudgeAdmin.Core.Domain.Models;

// One login or submission of a contest participant, with the IP it came from.
public sealed record ExamActivityEvent(string UserId, string Ip, DateTime Time, string Source);

public sealed class ExamActivity
{
    public IReadOnlyCollection<string> ParticipantUserIds { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Nicks { get; init; } = new Dictionary<string, string>();

    public IReadOnlyCollection<ExamActivityEvent> Events { get; init; } = Array.Empty<ExamActivityEvent>();
}

public static class ExamActivitySources
{
    public const string Login = "login";
    public const string Submission = "submission";
}

public static class ExamAlertCodes
{
    public const string OutsideLab = "OUTSIDE_LAB";
    public const string ConcurrentUse = "CONCURRENT_USE";
    public const string MultipleIps = "MULTIPLE_IPS";
    public const string SharedIp = "SHARED_IP";
}

public sealed class ExamMonitorResponse
{
    public int ContestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public IReadOnlyCollection<string> LabIps { get; set; } = Array.Empty<string>();

    public DateTime GeneratedAt { get; set; }

    public IReadOnlyCollection<ExamMonitorAlert> Alerts { get; set; } = Array.Empty<ExamMonitorAlert>();

    public IReadOnlyCollection<ExamMonitorParticipant> Participants { get; set; } = Array.Empty<ExamMonitorParticipant>();
}

public sealed class ExamMonitorAlert
{
    public string Code { get; set; } = string.Empty;

    // "high" or "medium".
    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyCollection<string> UserIds { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Ips { get; set; } = Array.Empty<string>();
}

public sealed class ExamMonitorParticipant
{
    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public IReadOnlyCollection<string> AlertCodes { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<ExamMonitorIpUsage> Ips { get; set; } = Array.Empty<ExamMonitorIpUsage>();
}

public sealed class ExamMonitorIpUsage
{
    public string Ip { get; set; } = string.Empty;

    public bool IsLab { get; set; }

    public int Logins { get; set; }

    public int Submissions { get; set; }

    public DateTime FirstSeen { get; set; }

    public DateTime LastSeen { get; set; }
}
