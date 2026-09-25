using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class PublicRepository
{
    public async Task<PublicContestsResponse> GetContestsAsync(int siteId, string? status, string? level, string? sortBy, int page, int pageSize, string? searchTerm)
    {
        var updatedAtUtc = DateTime.Now;
        var contestNow = DateTime.Now;
        var normalizedStatus = (status?.Trim().ToLowerInvariant() ?? "all") switch
        {
            "active" => "ACTIVE",
            "upcoming" => "UPCOMING",
            "past" => "FINISHED",
            "gym" => "GYM",
            _ => "ALL"
        };
        var normalizedLevel = (level?.Trim().ToLowerInvariant() ?? "all") switch
        {
            "regional" => "REGIONAL",
            "national" => "NATIONAL",
            "practice" => "PRACTICE",
            "training" => "TRAINING",
            _ => "ALL"
        };
        var normalizedSort = (sortBy?.Trim().ToLowerInvariant() ?? "date_desc") == "date_asc" ? "DATE_ASC" : "DATE_DESC";
        var normalizedSearch = searchTerm?.Trim() ?? string.Empty;

        var contests = await (
            from contestSite in _context.ContestSites
            join contest in _context.Contests on contestSite.ContestId equals contest.ContestId
            where contestSite.SiteId == siteId && (contest.Defunct == "N" || contest.Defunct == "O")
            select new ContestProjection
            {
                ContestId = contest.ContestId,
                Title = string.IsNullOrWhiteSpace(contest.Title) ? $"Contest #{contest.ContestId}" : contest.Title,
                StartTimeUtc = contest.StartTime,
                EndTimeUtc = contest.EndTime,
                IsPrivate = contest.Private != 0,
                Track = contest.Track,
                Level = contest.Level,
                IsPromoted = contest.Defunct == "O"
            }
        ).ToListAsync();

        contests = normalizedStatus == "GYM"
            ? contests.Where(item => item.IsPromoted).ToList()
            : contests.Where(item => !item.IsPromoted).ToList();

        var contestIds = contests.Select(item => item.ContestId).ToList();
        var problemCounts = await _context.ContestProblems
            .Where(item => item.ContestId.HasValue && contestIds.Contains(item.ContestId.Value))
            .GroupBy(item => item.ContestId!.Value)
            .Select(group => new { ContestId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ContestId, item => item.Count);

        var participantCounts = await _context.ContestUsers
            .Where(item => item.SiteId == siteId && contestIds.Contains(item.ContestId))
            .GroupBy(item => item.ContestId)
            .Select(group => new { ContestId = group.Key, Count = group.Select(item => item.UserId).Distinct().Count() })
            .ToDictionaryAsync(item => item.ContestId, item => item.Count);

        var items = contests
            .Select(contest =>
            {
                var computedStatus = ComputeContestStatus(contest.StartTimeUtc, contest.EndTimeUtc, contestNow, contest.IsPromoted);
                var computedTrack = string.IsNullOrWhiteSpace(contest.Track) ? "GENERAL" : contest.Track;
                var computedLevel = string.IsNullOrWhiteSpace(contest.Level)
                    ? "PRACTICE"
                    : contest.Level;

                return new PublicContestItem
                {
                    ContestId = contest.ContestId,
                    Title = contest.Title,
                    StartTimeUtc = contest.StartTimeUtc,
                    EndTimeUtc = contest.EndTimeUtc,
                    Status = computedStatus,
                    Track = computedTrack,
                    Level = computedLevel,
                    IsPrivate = contest.IsPrivate,
                    Obi = computedTrack == "OBI",
                    IsPromoted = contest.IsPromoted,
                    DurationMinutes = Math.Max(1, (int)Math.Round((contest.EndTimeUtc - contest.StartTimeUtc).TotalMinutes)),
                    ProblemCount = problemCounts.TryGetValue(contest.ContestId, out var problemCount) ? problemCount : 0,
                    ParticipantCount = participantCounts.TryGetValue(contest.ContestId, out var participantCount) ? participantCount : 0
                };
            })
            .Where(item =>
            {
                if (normalizedStatus != "ALL" && normalizedStatus != "GYM" && item.Status != normalizedStatus)
                {
                    return false;
                }

                if (normalizedLevel != "ALL" && item.Level != normalizedLevel)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(normalizedSearch)
                    && !item.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    && !item.ContestId.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            })
            .ToList();

        items = normalizedSort == "DATE_ASC"
            ? items.OrderBy(item => item.StartTimeUtc).ThenBy(item => item.ContestId).ToList()
            : items.OrderByDescending(item => item.StartTimeUtc).ThenByDescending(item => item.ContestId).ToList();

        var pagedItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PublicContestsResponse
        {
            SiteId = siteId,
            Total = items.Count,
            Page = page,
            PageSize = pageSize,
            FilterStatus = normalizedStatus,
            FilterLevel = normalizedLevel,
            SortBy = normalizedSort,
            UpdatedAtUtc = updatedAtUtc,
            Items = pagedItems
        };
    }

    public async Task<ContestReportResponse> GetContestReportAsync(int siteId, int contestId, CurrentUser? currentUser = null)
    {
        var contest = await GetContestAsync(siteId, contestId);
        await CheckContestAccessAsync(siteId, contest, currentUser);
        var generatedAtUtc = DateTime.Now;
        var contestNow = DateTime.Now;

        var problemCount = await _context.ContestProblems
            .Where(item => item.ContestId == contestId)
            .CountAsync();

        var participantIds = await _context.ContestUsers
            .Where(item => item.SiteId == siteId && item.ContestId == contestId)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync();
        var participantIdSet = participantIds.ToHashSet(StringComparer.Ordinal);
        var isPromotedContest = contest.Defunct == "O";

        var submissionStats = await OfficialSolutions()
            .Where(solution => solution.SiteId == siteId && solution.ContestId == contestId)
            .GroupBy(solution => solution.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Submissions = group.Count(),
                Accepted = group.Count(solution => solution.Result == AcceptedResultCode),
                Solved = group.Where(solution => solution.Result == AcceptedResultCode)
                    .Select(solution => solution.ProblemId)
                    .Distinct()
                    .Count(),
                FirstSubmitUtc = group.Min(solution => (DateTime?)solution.InDate),
                LastSubmitUtc = group.Max(solution => (DateTime?)solution.InDate)
            })
            .ToListAsync();

        var userIds = submissionStats
            .Select(item => item.UserId)
            .Distinct()
            .ToList();

        var profiles = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => new
                {
                    Nick = string.IsNullOrWhiteSpace(profile.Nick) ? profile.UserId : profile.Nick,
                    School = profile.School ?? string.Empty
                });

        var items = userIds
            .Select(userId =>
            {
                var stats = submissionStats.FirstOrDefault(item => item.UserId == userId);
                var submissions = stats?.Submissions ?? 0;
                var accepted = stats?.Accepted ?? 0;
                var solved = stats?.Solved ?? 0;

                profiles.TryGetValue(userId, out var profile);

                return new ContestReportItem
                {
                    UserId = userId,
                    Nick = profile?.Nick ?? userId,
                    School = profile?.School ?? string.Empty,
                    Solved = solved,
                    Submissions = submissions,
                    Accepted = accepted,
                    Accuracy = submissions == 0 ? 0 : Math.Round((decimal)accepted * 100m / submissions, 2),
                    IsVirtualParticipant = isPromotedContest && participantIdSet.Contains(userId),
                    FirstSubmitUtc = stats?.FirstSubmitUtc,
                    LastSubmitUtc = stats?.LastSubmitUtc
                };
            })
            .OrderByDescending(item => item.Solved)
            .ThenByDescending(item => item.Accepted)
            .ThenBy(item => item.Submissions)
            .ThenBy(item => item.UserId)
            .ToList();

        for (var index = 0; index < items.Count; index++)
        {
            items[index].Rank = index + 1;
        }

        var isOwner = await IsContestOwnerAsync(siteId, contestId, currentUser?.UserId);
        var canDownloadCsv = currentUser is not null
            && currentUser.SiteId == siteId
            && (currentUser.Role == UserRolesEnum.Administrador
                || currentUser.Role == UserRolesEnum.Docente
                || currentUser.Role == UserRolesEnum.Auxiliar);

        var contestTrack = string.IsNullOrWhiteSpace(contest.Track) ? "GENERAL" : contest.Track;
        var contestLevel = string.IsNullOrWhiteSpace(contest.Level) ? "PRACTICE" : contest.Level;

        return new ContestReportResponse
        {
            ContestId = contest.ContestId,
            Title = string.IsNullOrWhiteSpace(contest.Title) ? $"Contest #{contest.ContestId}" : contest.Title,
            Description = contest.Description ?? string.Empty,
            StartTimeUtc = contest.StartTime,
            EndTimeUtc = contest.EndTime,
            Status = ComputeContestStatus(contest.StartTime, contest.EndTime, contestNow, isPromotedContest),
            Track = contestTrack,
            Level = contestLevel,
            SiteId = siteId,
            GeneratedAtUtc = generatedAtUtc,
            DurationMinutes = Math.Max(1, (int)Math.Round((contest.EndTime - contest.StartTime).TotalMinutes)),
            IsPrivate = contest.Private != 0,
            IsPromoted = isPromotedContest,
            ProblemCount = problemCount,
            ParticipantCount = participantIds.Count,
            TotalSubmissions = submissionStats.Sum(item => item.Submissions),
            TotalAccepted = submissionStats.Sum(item => item.Accepted),
            IsOwner = isOwner,
            CanDownloadCsv = canDownloadCsv,
            Items = items
        };
    }

    public async Task<bool> CanDownloadContestReportCsvAsync(CurrentUser currentUser, int siteId, int contestId)
    {
        await GetContestAsync(siteId, contestId);

        if (currentUser.SiteId != siteId)
        {
            return false;
        }

        if (currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar)
        {
            return true;
        }

        return false;
    }

    public async Task RegisterForContestAsync(CurrentUser currentUser, int siteId, int contestId)
    {
        var contest = await GetContestAsync(siteId, contestId);
        await CheckContestAccessAsync(siteId, contest, currentUser);

        var alreadyRegistered = await _context.ContestUsers
            .AnyAsync(item => item.SiteId == siteId && item.ContestId == contestId && item.UserId == currentUser.UserId);

        if (alreadyRegistered)
        {
            return;
        }

        _context.ContestUsers.Add(new DbContestUser
        {
            ContestId = contestId,
            UserId = currentUser.UserId,
            SiteId = siteId,
            IsOwner = false
        });

        await _context.SaveChangesAsync();
    }

    private async Task<Dictionary<int, ContestProblemReference>> LoadContestProblemMapAsync(int contestId)
    {
        return await _context.ContestProblems
            .Where(item => item.ContestId == contestId && item.ProblemId.HasValue && item.Num.HasValue && item.Num.Value >= 0)
            .Select(item => new ContestProblemReference
            {
                ContestId = contestId,
                ProblemId = item.ProblemId!.Value,
                Num = item.Num!.Value,
                ContestProblemId = ContestProblemCode.FromNumber(item.Num!.Value)
            })
            .ToDictionaryAsync(item => item.ProblemId);
    }

    private async Task<ContestProblemReference> ResolveContestProblemReferenceAsync(int siteId, int contestId, string contestProblemId, CurrentUser? currentUser = null)
    {
        var contest = await GetContestAsync(siteId, contestId);
        await CheckContestAccessAsync(siteId, contest, currentUser);

        if (!ContestProblemCode.TryParse(contestProblemId, out var contestProblemNum))
        {
            throw new ArgumentException("Problema del concurso inválido.");
        }

        var contestProblem = await (
            from item in _context.ContestProblems
            join problem in _context.Problems on item.ProblemId equals problem.ProblemId
            where item.ContestId == contestId
                && item.ProblemId.HasValue
                && item.Num.HasValue
                && item.Num.Value == contestProblemNum
                && problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive)
                && (problem.Defunct == "N" || problem.Defunct == "Y" || problem.Defunct == "O")
            select new ContestProblemReference
            {
                ContestId = contestId,
                ProblemId = item.ProblemId!.Value,
                Num = item.Num!.Value,
                ContestProblemId = ContestProblemCode.FromNumber(item.Num!.Value)
            })
            .FirstOrDefaultAsync();

        if (contestProblem == null)
        {
            throw new ArgumentException("Problema del concurso inválido o no disponible.");
        }

        return contestProblem;
    }

    private async Task<DbContest> GetContestAsync(int siteId, int contestId)
    {
        var contest = await (
            from contestSite in _context.ContestSites
            join item in _context.Contests on contestSite.ContestId equals item.ContestId
            where contestSite.SiteId == siteId && item.ContestId == contestId && (item.Defunct == "N" || item.Defunct == "O")
            select item
        ).FirstOrDefaultAsync();

        if (contest == null)
        {
            throw new ArgumentException("Concurso no encontrado.");
        }

        return contest;
    }

    private async Task CheckContestAccessAsync(int siteId, DbContest contest, CurrentUser? currentUser)
    {
        if (contest.Private == 0)
        {
            return;
        }

        if (!await CanAccessPrivateContestAsync(siteId, contest.ContestId, currentUser))
        {
            throw new UnauthorizedAccessException("This contest is private.");
        }
    }

    private async Task<bool> CanAccessPrivateContestAsync(int siteId, int contestId, CurrentUser? currentUser)
    {
        if (currentUser == null || currentUser.SiteId != siteId)
        {
            return false;
        }

        if (currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar)
        {
            return true;
        }

        return await _context.ContestUsers
            .AnyAsync(item =>
                item.SiteId == siteId
                && item.ContestId == contestId
                && item.UserId == currentUser.UserId);
    }

    private async Task<bool> IsContestOwnerAsync(int siteId, int contestId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await _context.ContestUsers
            .AnyAsync(item =>
                item.SiteId == siteId
                && item.ContestId == contestId
                && item.UserId == userId
                && item.IsOwner);
    }

    private static string ComputeContestStatus(DateTime startTimeUtc, DateTime endTimeUtc, DateTime nowUtc, bool isPromoted = false)
    {
        if (startTimeUtc > nowUtc)
        {
            return "UPCOMING";
        }

        // Un contest oficial/promovido (defunct == "O") es de práctica abierta: una vez que arranca, no vence.
        if (isPromoted || endTimeUtc >= nowUtc)
        {
            return "ACTIVE";
        }

        return "FINISHED";
    }

    private static void CheckContestOpen(DbContest contest)
    {
        if (contest.Defunct == "O")
        {
            return;
        }

        var now = DateTime.Now;

        if (contest.StartTime > now)
        {
            throw new InvalidOperationException("Este concurso todavía no acepta envíos.");
        }

        if (contest.EndTime < now)
        {
            throw new InvalidOperationException("Este concurso ya finalizó y no acepta envíos.");
        }
    }
}
