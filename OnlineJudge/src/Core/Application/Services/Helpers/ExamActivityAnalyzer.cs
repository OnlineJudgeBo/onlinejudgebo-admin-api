using System.Net;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Helpers;

// Turns raw login/submission IPs of an exam into per-participant usage plus alerts.
// An IP is only a hint (mobile data changes IPs, labs share one public IP), so alerts flag
// what a proctor should look at, never a verdict.
public static class ExamActivityAnalyzer
{
    private const string LabKey = "LAB";
    private static readonly TimeSpan ConcurrentActivityWindow = TimeSpan.FromMinutes(5);

    // Docker's default address pool: seeing it means the judge stored the proxy's IP, not the student's.
    private static readonly IPNetwork DockerNetwork = IPNetwork.Parse("172.16.0.0/12");

    public static IReadOnlyList<IPNetwork> ParseLabIps(string? labIps)
    {
        var networks = new List<IPNetwork>();
        foreach (var entry in SplitLabIps(labIps))
        {
            if (IPNetwork.TryParse(entry, out var network))
            {
                networks.Add(network);
            }
            else if (IPAddress.TryParse(entry, out var address))
            {
                networks.Add(new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
            }
            else
            {
                throw new ArgumentException($"IP o rango de laboratorio inválido: {entry}");
            }
        }

        return networks;
    }

    // Validates and returns the canonical "a, b/24" form stored in contest.exam_lab_ips (null when empty).
    public static string? NormalizeLabIps(string? labIps)
    {
        ParseLabIps(labIps);
        var entries = SplitLabIps(labIps).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return entries.Count == 0 ? null : string.Join(", ", entries);
    }

    public static ExamMonitorResponse Analyze(Contest contest, ExamActivity activity, DateTime now)
    {
        var labNetworks = ParseLabIps(contest.ExamLabIps);
        var events = activity.Events
            .Where(item => IPAddress.TryParse(item.Ip, out _))
            .OrderBy(item => item.Time)
            .ToList();
        bool IsLab(string ip) => labNetworks.Any(network => network.Contains(IPAddress.Parse(ip)));
        bool IsProxy(string ip) => !IsLab(ip) && DockerNetwork.Contains(IPAddress.Parse(ip));
        // Proxy IPs say nothing about where the student was: listed, but left out of every alert.
        var alertEvents = events.Where(item => !IsProxy(item.Ip)).ToList();
        var alertEventsByUser = alertEvents
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        string Key(string ip) => IsLab(ip) ? LabKey : ip;

        var participantIds = activity.ParticipantUserIds
            .Concat(events.Select(item => item.UserId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(userId => userId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var eventsByUser = events
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var alerts = new List<ExamMonitorAlert>();
        var alertCodesByUser = participantIds.ToDictionary(userId => userId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        void AddAlert(string code, string severity, string message, IEnumerable<string> userIds, IEnumerable<string> ips)
        {
            var users = userIds.ToList();
            alerts.Add(new ExamMonitorAlert { Code = code, Severity = severity, Message = message, UserIds = users, Ips = ips.ToList() });
            foreach (var userId in users)
            {
                alertCodesByUser[userId].Add(code);
            }
        }

        foreach (var userId in participantIds)
        {
            var userEvents = alertEventsByUser.GetValueOrDefault(userId) ?? new List<ExamActivityEvent>();
            var outsideIps = userEvents.Select(item => item.Ip).Where(ip => !IsLab(ip)).Distinct().ToList();

            if (labNetworks.Count > 0 && outsideIps.Count > 0)
            {
                AddAlert(ExamAlertCodes.OutsideLab, "high", $"{userId} tuvo actividad desde fuera del laboratorio.", new[] { userId }, outsideIps);
            }

            if (HasConcurrentActivity(userEvents, Key))
            {
                AddAlert(ExamAlertCodes.ConcurrentUse, "high", $"{userId} tuvo actividad desde IPs distintas con 5 minutos o menos de diferencia.", new[] { userId }, userEvents.Select(item => item.Ip).Distinct());
            }
            else if (labNetworks.Count == 0 && userEvents.Select(item => Key(item.Ip)).Distinct().Count() > 1)
            {
                var ips = userEvents.Select(item => item.Ip).Distinct().ToList();
                AddAlert(ExamAlertCodes.MultipleIps, "medium", $"{userId} usó {ips.Count} IPs distintas durante el examen.", new[] { userId }, ips);
            }
        }

        foreach (var group in alertEvents
            .Where(item => !IsLab(item.Ip))
            .GroupBy(item => item.Ip)
            .Select(group => new { Ip = group.Key, Users = group.Select(item => item.UserId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(userId => userId).ToList() })
            .Where(group => group.Users.Count > 1)
            .OrderBy(group => group.Ip))
        {
            AddAlert(ExamAlertCodes.SharedIp, "medium", $"{group.Users.Count} cuentas usaron la misma IP {group.Ip}.", group.Users, new[] { group.Ip });
        }

        var dockerIps = events.Select(item => item.Ip).Distinct().Where(IsProxy).ToList();
        if (dockerIps.Count > 0)
        {
            alerts.Insert(0, new ExamMonitorAlert
            {
                Code = "IP_CAPTURE",
                Severity = "medium",
                Message = "Algunas IPs son de la red interna de Docker (proxy): el juez no está registrando la IP real del estudiante.",
                Ips = dockerIps
            });
        }

        return new ExamMonitorResponse
        {
            ContestId = contest.ContestId,
            Title = contest.Title ?? $"Contest #{contest.ContestId}",
            StartTime = contest.StartTime,
            EndTime = contest.EndTime,
            LabIps = SplitLabIps(contest.ExamLabIps).ToList(),
            GeneratedAt = now,
            Alerts = alerts,
            Participants = participantIds
                .Select(userId => new ExamMonitorParticipant
                {
                    UserId = userId,
                    Nick = activity.Nicks.GetValueOrDefault(userId) is { Length: > 0 } nick ? nick : userId,
                    AlertCodes = alertCodesByUser[userId].Distinct().ToList(),
                    Ips = (eventsByUser.GetValueOrDefault(userId) ?? new List<ExamActivityEvent>())
                        .GroupBy(item => item.Ip)
                        .Select(group => new ExamMonitorIpUsage
                        {
                            Ip = group.Key,
                            IsLab = IsLab(group.Key),
                            Logins = group.Count(item => item.Source == ExamActivitySources.Login),
                            Submissions = group.Count(item => item.Source == ExamActivitySources.Submission),
                            FirstSeen = group.Min(item => item.Time),
                            LastSeen = group.Max(item => item.Time)
                        })
                        .OrderBy(usage => usage.FirstSeen)
                        .ToList()
                })
                .OrderByDescending(participant => participant.AlertCodes.Count)
                .ThenBy(participant => participant.UserId, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static bool HasConcurrentActivity(IReadOnlyList<ExamActivityEvent> events, Func<string, string> key) =>
        events.Zip(events.Skip(1), (first, second) =>
                key(first.Ip) != key(second.Ip) && second.Time - first.Time <= ConcurrentActivityWindow)
            .Any(concurrent => concurrent);

    private static IEnumerable<string> SplitLabIps(string? labIps) =>
        (labIps ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
