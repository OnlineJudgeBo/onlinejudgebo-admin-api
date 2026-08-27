using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class PublicRepository
{
    public async Task<PublicDashboardResponse> GetDashboardAsync(int siteId)
    {
        var generatedAtUtc = DateTime.Now;
        var contestNow = DateTime.Now;
        var fromDate = generatedAtUtc.AddDays(-30);

        var problemCount = await _context.ProblemSites.CountAsync(problemSite => problemSite.SiteId == siteId && problemSite.IsActive);

        var activeUsers = await OfficialSolutions()
            .Where(solution => solution.SiteId == siteId && solution.InDate >= fromDate)
            .Select(solution => solution.UserId)
            .Distinct()
            .CountAsync();

        var submissionsLast30Days = await OfficialSolutions()
            .Where(solution => solution.SiteId == siteId && solution.InDate >= fromDate)
            .CountAsync();

        var activeContests = await (
            from contestSite in _context.ContestSites
            join contest in _context.Contests on contestSite.ContestId equals contest.ContestId
            where contestSite.SiteId == siteId
                && contest.Defunct == "N"
                && contest.StartTime <= contestNow
                && contest.EndTime >= contestNow
            select contest.ContestId
        ).CountAsync();

        var upcomingContests = await (
            from contestSite in _context.ContestSites
            join contest in _context.Contests on contestSite.ContestId equals contest.ContestId
            where contestSite.SiteId == siteId
                && contest.Defunct == "N"
                && contest.StartTime > contestNow
            orderby contest.StartTime
            select new UpcomingContest
            {
                ContestId = contest.ContestId,
                Title = string.IsNullOrWhiteSpace(contest.Title) ? $"Contest #{contest.ContestId}" : contest.Title,
                StartTimeUtc = contest.StartTime,
                EndTimeUtc = contest.EndTime
            }
        )
        .Take(5)
        .ToListAsync();

        var trendingTopics = await _context.Classifications
            .Select(classification => new
            {
                classification.Name,
                Total = classification.Problems!.Count(problem => problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive))
            })
            .Where(item => item.Total > 0)
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Name)
            .Take(8)
            .Select(item => item.Name)
            .ToListAsync();

        return new PublicDashboardResponse
        {
            SiteId = siteId,
            GeneratedAtUtc = generatedAtUtc,
            Metrics = new DashboardMetricSummary
            {
                Problems = problemCount,
                ActiveUsers = activeUsers,
                SubmissionsLast30Days = submissionsLast30Days,
                ActiveContests = activeContests
            },
            TrendingTopics = trendingTopics,
            UpcomingContests = upcomingContests
        };
    }

    public async Task<PublicRankingResponse> GetRankingAsync(int siteId, int limit, string? scope)
    {
        var today = DateTime.Now.Date;
        var startDate = (scope ?? "all").Trim().ToLowerInvariant() switch
        {
            "d" => today,
            "w" => today.AddDays(-6),
            "m" => new DateTime(today.Year, today.Month, 1),
            _ => DateTime.MinValue
        };

        var rankingRows = await OfficialSolutions()
            .Where(solution => solution.SiteId == siteId && solution.InDate >= startDate)
            .GroupBy(solution => solution.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Solved = group.Where(solution => solution.Result == AcceptedResultCode)
                    .Select(solution => solution.ProblemId)
                    .Distinct()
                    .Count(),
                Submit = group.Count()
            })
            .OrderByDescending(item => item.Solved)
            .ThenBy(item => item.Submit)
            .ThenBy(item => item.UserId)
            .Take(limit)
            .ToListAsync();

        var userIds = rankingRows.Select(item => item.UserId).ToList();
        var profiles = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => new
                {
                    Nick = string.IsNullOrWhiteSpace(profile.Nick) ? profile.UserId : profile.Nick,
                    School = profile.School ?? string.Empty
                });

        var items = rankingRows
            .Select((item, index) =>
            {
                profiles.TryGetValue(item.UserId, out var profile);

                return new PublicRankingItem
                {
                    Rank = index + 1,
                    UserId = item.UserId,
                    Nick = profile?.Nick ?? item.UserId,
                    School = profile?.School ?? string.Empty,
                    Solved = item.Solved,
                    Submit = item.Submit,
                    Ratio = item.Submit == 0 ? 0 : Math.Round((decimal)item.Solved * 100m / item.Submit, 2)
                };
            })
            .ToList();

        return new PublicRankingResponse
        {
            SiteId = siteId,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

    public async Task<IReadOnlyCollection<PublicLanguageItem>> GetLanguagesAsync()
    {
        return await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && !string.IsNullOrWhiteSpace(language.Name))
            .OrderBy(language => language.LanguageId)
            .Select(language => new PublicLanguageItem
            {
                LanguageId = language.LanguageId!.Value,
                Name = language.Name!
            })
            .ToListAsync();
    }

    public async Task<PublicOnlineUsersResponse> GetOnlineUsersAsync(int siteId, int windowMinutes)
    {
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        var fromTimestamp = now - windowMinutes * 60L;

        var onlineRows = await _context.Online
            .Where(item => item.Lastmove >= fromTimestamp)
            .OrderByDescending(item => item.Lastmove)
            .Take(200)
            .ToListAsync();

        var items = onlineRows
            .Select(item => new PublicOnlineUserItem
            {
                Hash = item.Hash,
                UserAgent = item.Ua,
                Referer = item.Refer,
                Uri = item.Uri,
                FirstSeenUtc = item.Firsttime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(item.Firsttime.Value).LocalDateTime : null,
                LastSeenUtc = DateTimeOffset.FromUnixTimeSeconds(item.Lastmove).LocalDateTime
            })
            .ToList();

        return new PublicOnlineUsersResponse
        {
            SiteId = siteId,
            WindowMinutes = windowMinutes,
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

    private async Task<List<PublicLearningPathPhase>> BuildLearningPathPhasesAsync()
    {
        var learningPath = await _academicContext.LearningPaths
            .OrderBy(path => path.LearningPathId)
            .FirstOrDefaultAsync();

        if (learningPath == null)
        {
            return new List<PublicLearningPathPhase>();
        }

        var linkedTopicIds = await _academicContext.LearningPathTopics
            .Where(item => item.LearningPathId == learningPath.LearningPathId)
            .Select(item => item.TopicId)
            .ToListAsync();

        if (!linkedTopicIds.Any())
        {
            return new List<PublicLearningPathPhase>();
        }

        var topics = await _academicContext.Topics
            .Where(topic => linkedTopicIds.Contains(topic.TopicId))
            .ToDictionaryAsync(topic => topic.TopicId, topic => topic.Name);

        var subtopics = await _academicContext.Subtopics
            .Where(subtopic => linkedTopicIds.Contains(subtopic.TopicId))
            .OrderBy(subtopic => subtopic.TopicId)
            .ThenBy(subtopic => subtopic.SortOrder)
            .Select(subtopic => new
            {
                subtopic.TopicId,
                subtopic.Title,
                subtopic.DifficultyBand
            })
            .ToListAsync();

        return linkedTopicIds
            .Distinct()
            .Select((topicId, index) =>
            {
                var modules = subtopics
                    .Where(subtopic => subtopic.TopicId == topicId)
                    .Select(subtopic => subtopic.Title)
                    .Take(5)
                    .ToList();

                return new PublicLearningPathPhase
                {
                    Id = $"stage_{index + 1}",
                    Title = topics.TryGetValue(topicId, out var title) ? title : $"Etapa {index + 1}",
                    Objective = modules.FirstOrDefault() ?? "Progresion de tecnicas competitivas.",
                    Modules = modules,
                    MinimumProblems = Math.Max(3, modules.Count * 2)
                };
            })
            .ToList();
    }
}
