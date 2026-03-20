using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class PublicRepository : IPublicRepository
{
    private const short AcceptedResultCode = 4;
    private static readonly TimeZoneInfo ContestTimeZone = ResolveContestTimeZone();

    private readonly AppDbContext _context;
    private readonly AcademicCatalogDbContext _academicContext;

    public PublicRepository(AppDbContext context, AcademicCatalogDbContext academicContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _academicContext = academicContext ?? throw new ArgumentNullException(nameof(academicContext));
    }

    public async Task<PublicDashboardResponse> GetDashboardAsync(int siteId)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var contestNow = GetContestClockNow();
        var fromDate = generatedAtUtc.AddDays(-30);

        var problemCount = await _context.ProblemSites.CountAsync(problemSite => problemSite.SiteId == siteId);

        var activeUsers = await _context.Solutions
            .Where(solution => solution.SiteId == siteId && solution.InDate >= fromDate)
            .Select(solution => solution.UserId)
            .Distinct()
            .CountAsync();

        var submissionsLast30Days = await _context.Solutions
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
                Total = classification.Problems!.Count(problem => problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId))
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

    public async Task<PublicProblemsResponse> GetProblemsAsync(int siteId, int page, int pageSize, string? searchTerm, int? year, string? contestTrack, string? source, string? tag, string? sortBy, int? contestId, CurrentUser? currentUser = null)
    {
        var normalizedSearch = searchTerm?.Trim() ?? string.Empty;
        var normalizedSource = source?.Trim() ?? string.Empty;
        if (string.Equals(normalizedSource, "all", StringComparison.OrdinalIgnoreCase))
        {
            normalizedSource = string.Empty;
        }

        var normalizedTag = tag?.Trim() ?? string.Empty;
        var normalizedTrack = (contestTrack?.Trim().ToLowerInvariant() ?? string.Empty) switch
        {
            "obi" => "OBI",
            "icpc_bolivia" => "ICPC_BOLIVIA",
            "general" => "GENERAL",
            _ => string.Empty
        };
        var normalizedSort = (sortBy?.Trim().ToLowerInvariant() ?? "default") switch
        {
            "id_asc" => "id_asc",
            "id_desc" => "id_desc",
            "status_asc" => "status_asc",
            "status_desc" => "status_desc",
            "accepted_asc" => "accepted_asc",
            "accepted_desc" => "accepted_desc",
            "submit_asc" => "submit_asc",
            "submit_desc" => "submit_desc",
            "success_rate_asc" => "success_rate_asc",
            "success_rate_desc" => "success_rate_desc",
            _ => "default"
        };

        if (contestId.HasValue)
        {
            await EnsureContestExistsAsync(siteId, contestId.Value);
        }

        var contestProblemMap = contestId.HasValue
            ? await LoadContestProblemMapAsync(contestId.Value)
            : null;

        var problems = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue
                && problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId)
                && (!contestId.HasValue || _context.ContestProblems.Any(contestProblem =>
                    contestProblem.ContestId == contestId.Value && contestProblem.ProblemId == problem.ProblemId))
                && (problem.Defunct == "N" || problem.Defunct == "Y"))
            .Select(problem => new ProblemListProjection
            {
                ProblemId = problem.ProblemId!.Value,
                Title = problem.Title,
                Description = problem.Description ?? string.Empty,
                Accepted = problem.Accepted ?? 0,
                Submit = problem.Submit ?? 0,
                Solved = problem.Solved ?? 0,
                PublishedAt = problem.InDate
            })
            .ToListAsync();

        if (contestId.HasValue && problems.Count > 0)
        {
            var contestProblemIds = problems.Select(problem => problem.ProblemId).Distinct().ToList();
            var contestProblemStats = await _context.Solutions
                .Where(solution =>
                    solution.SiteId == siteId
                    && solution.ContestId == contestId.Value
                    && contestProblemIds.Contains(solution.ProblemId))
                .GroupBy(solution => solution.ProblemId)
                .Select(group => new
                {
                    ProblemId = group.Key,
                    Accepted = group.Count(solution => solution.Result == AcceptedResultCode),
                    Submit = group.Count()
                })
                .ToDictionaryAsync(item => item.ProblemId, item => new { item.Accepted, item.Submit });

            foreach (var problem in problems)
            {
                if (contestProblemStats.TryGetValue(problem.ProblemId, out var stats))
                {
                    problem.Accepted = stats.Accepted;
                    problem.Submit = stats.Submit;
                    continue;
                }

                problem.Accepted = 0;
                problem.Submit = 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            problems = problems
                .Where(problem =>
                    problem.ProblemId.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || (contestProblemMap?.GetValueOrDefault(problem.ProblemId)?.ContestProblemId?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                    || problem.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || problem.Description.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var problemMetadata = await LoadProblemMetadataAsync(siteId, problems.Select(problem => problem.ProblemId).ToList());

        var filteredProblems = problems
            .Where(problem =>
            {
                var metadata = problemMetadata.GetValueOrDefault(problem.ProblemId, ProblemMetadata.Empty);
                var effectiveYears = metadata.Years.Count > 0
                    ? metadata.Years
                    : problem.PublishedAt.HasValue
                        ? new HashSet<int> { problem.PublishedAt.Value.Year }
                        : new HashSet<int>();

                if (year.HasValue && !effectiveYears.Contains(year.Value))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(normalizedTrack) && !metadata.Tracks.Contains(normalizedTrack))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(normalizedSource) && !metadata.Sources.Contains(normalizedSource))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(normalizedTag) && !metadata.Tags.Contains(normalizedTag))
                {
                    return false;
                }

                return true;
            })
            .OrderByDescending(problem => problem.ProblemId)
            .ToList();

        HashSet<int>? solvedProblemIdsForFilteredProblems = null;
        HashSet<int>? attemptedProblemIdsForFilteredProblems = null;
        if (normalizedSort is "status_asc" or "status_desc")
        {
            var filteredProblemIds = filteredProblems.Select(problem => problem.ProblemId).ToList();
            solvedProblemIdsForFilteredProblems = await LoadSolvedProblemIdsAsync(siteId, currentUser, filteredProblemIds, contestId);
            attemptedProblemIdsForFilteredProblems = await LoadAttemptedProblemIdsAsync(siteId, currentUser, filteredProblemIds, contestId);
        }

        filteredProblems = normalizedSort switch
        {
            "default" when contestId.HasValue => filteredProblems
                .OrderBy(problem => contestProblemMap?.GetValueOrDefault(problem.ProblemId)?.Num ?? int.MaxValue)
                .ThenBy(problem => problem.ProblemId)
                .ToList(),
            "id_asc" => filteredProblems.OrderBy(problem => problem.ProblemId).ToList(),
            "id_desc" => filteredProblems.OrderByDescending(problem => problem.ProblemId).ToList(),
            "status_asc" => filteredProblems
                .OrderBy(problem => GetProblemUserStatusSortRank(problem.ProblemId, solvedProblemIdsForFilteredProblems, attemptedProblemIdsForFilteredProblems))
                .ThenBy(problem => contestProblemMap?.GetValueOrDefault(problem.ProblemId)?.Num ?? int.MaxValue)
                .ThenByDescending(problem => problem.ProblemId)
                .ToList(),
            "status_desc" => filteredProblems
                .OrderByDescending(problem => GetProblemUserStatusSortRank(problem.ProblemId, solvedProblemIdsForFilteredProblems, attemptedProblemIdsForFilteredProblems))
                .ThenBy(problem => contestProblemMap?.GetValueOrDefault(problem.ProblemId)?.Num ?? int.MaxValue)
                .ThenByDescending(problem => problem.ProblemId)
                .ToList(),
            "accepted_asc" => filteredProblems.OrderBy(problem => problem.Accepted).ThenBy(problem => problem.ProblemId).ToList(),
            "accepted_desc" => filteredProblems.OrderByDescending(problem => problem.Accepted).ThenBy(problem => problem.ProblemId).ToList(),
            "submit_asc" => filteredProblems.OrderBy(problem => problem.Submit).ThenBy(problem => problem.ProblemId).ToList(),
            "submit_desc" => filteredProblems.OrderByDescending(problem => problem.Submit).ThenBy(problem => problem.ProblemId).ToList(),
            "success_rate_asc" => filteredProblems.OrderBy(problem => CalculateSuccessRate(problem.Accepted, problem.Submit)).ThenBy(problem => problem.ProblemId).ToList(),
            "success_rate_desc" => filteredProblems.OrderByDescending(problem => CalculateSuccessRate(problem.Accepted, problem.Submit)).ThenBy(problem => problem.ProblemId).ToList(),
            _ => filteredProblems.OrderByDescending(problem => problem.ProblemId).ToList()
        };

        var pagedProblems = filteredProblems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var problemIds = pagedProblems.Select(problem => problem.ProblemId).ToList();
        var solvedProblemIds = solvedProblemIdsForFilteredProblems ?? await LoadSolvedProblemIdsAsync(siteId, currentUser, problemIds, contestId);
        var attemptedProblemIds = attemptedProblemIdsForFilteredProblems ?? await LoadAttemptedProblemIdsAsync(siteId, currentUser, problemIds, contestId);

        var items = pagedProblems
            .Select(problem =>
            {
                var metadata = problemMetadata.GetValueOrDefault(problem.ProblemId, ProblemMetadata.Empty);
                var years = metadata.Years.Count > 0
                    ? metadata.Years.OrderByDescending(item => item).ToList()
                    : problem.PublishedAt.HasValue
                        ? new List<int> { problem.PublishedAt.Value.Year }
                        : new List<int>();

                return new PublicProblemItem
                {
                    ProblemId = problem.ProblemId,
                    ContestId = contestId,
                    ContestProblemId = contestProblemMap?.GetValueOrDefault(problem.ProblemId)?.ContestProblemId,
                    Title = problem.Title,
                    Accepted = problem.Accepted,
                    Submit = problem.Submit,
                    SuccessRate = CalculateSuccessRate(problem.Accepted, problem.Submit),
                    Difficulty = CalculateDifficulty(problem.Accepted, problem.Submit),
                    IsSolvedByCurrentUser = solvedProblemIds.Contains(problem.ProblemId),
                    IsAttemptedByCurrentUser = attemptedProblemIds.Contains(problem.ProblemId),
                    Tags = metadata.Tags.Take(3).ToList(),
                    Years = years,
                    ContestTracks = metadata.Tracks.ToList(),
                    OriginSource = metadata.OriginSource
                };
            })
            .ToList();

        return new PublicProblemsResponse
        {
            SiteId = siteId,
            Total = filteredProblems.Count,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    public async Task<PublicProblemDetailResponse> GetProblemDetailAsync(int siteId, int problemId)
    {
        var problem = await _context.Problems
            .FirstOrDefaultAsync(item => item.ProblemId == problemId
                && item.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId)
                && (item.Defunct == "N" || item.Defunct == "Y"));

        if (problem == null)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        return await BuildProblemDetailResponseAsync(siteId, problem, problemId);
    }

    public async Task<PublicProblemDetailResponse> GetContestProblemDetailAsync(int siteId, int contestId, string contestProblemId)
    {
        var contestProblem = await ResolveContestProblemReferenceAsync(siteId, contestId, contestProblemId);
        var problem = await _context.Problems
            .FirstOrDefaultAsync(item => item.ProblemId == contestProblem.ProblemId
                && item.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId)
                && (item.Defunct == "N" || item.Defunct == "Y"));

        if (problem == null)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        return await BuildProblemDetailResponseAsync(siteId, problem, contestProblem.ProblemId, contestId, contestProblem.ContestProblemId);
    }

    private async Task<PublicProblemDetailResponse> BuildProblemDetailResponseAsync(
        int siteId,
        DbProblem problem,
        int problemId,
        int? contestId = null,
        string? contestProblemId = null)
    {
        var metadata = (await LoadProblemMetadataAsync(siteId, new[] { problemId })).GetValueOrDefault(problemId, ProblemMetadata.Empty);
        var years = metadata.Years.Count > 0
            ? metadata.Years.OrderByDescending(item => item).ToList()
            : problem.InDate.HasValue
                ? new List<int> { problem.InDate.Value.Year }
                : new List<int>();

        var samples = new List<ProblemSampleCase>();
        if (!string.IsNullOrWhiteSpace(problem.SampleInput) || !string.IsNullOrWhiteSpace(problem.SampleOutput))
        {
            samples.Add(new ProblemSampleCase
            {
                Index = 1,
                Input = problem.SampleInput ?? string.Empty,
                Output = problem.SampleOutput ?? string.Empty
            });
        }

        return new PublicProblemDetailResponse
        {
            ProblemId = problemId,
            ContestId = contestId,
            ContestProblemId = contestProblemId,
            Title = problem.Title,
            Description = problem.Description ?? string.Empty,
            InputSpec = problem.Input ?? string.Empty,
            OutputSpec = problem.Output ?? string.Empty,
            Hint = problem.Hint ?? string.Empty,
            Author = problem.Source ?? string.Empty,
            PublishedAtUtc = problem.InDate,
            TimeLimitSeconds = Math.Round(problem.TimeLimit / 1000m, 2),
            MemoryLimitMb = Math.Max(1, problem.MemoryLimit / 1024),
            Score = 100,
            Accepted = problem.Accepted ?? 0,
            Submit = problem.Submit ?? 0,
            Solved = problem.Solved ?? 0,
            SuccessRate = CalculateSuccessRate(problem.Accepted ?? 0, problem.Submit ?? 0),
            Difficulty = CalculateDifficulty(problem.Accepted ?? 0, problem.Submit ?? 0),
            Tags = metadata.Tags,
            Years = years,
            ContestTracks = metadata.Tracks,
            OriginSource = metadata.OriginSource,
            SampleCases = samples
        };
    }

    public async Task<PublicProblemFiltersResponse> GetProblemFiltersAsync(int siteId)
    {
        var siteProblems = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId))
            .Select(problem => new { ProblemId = problem.ProblemId!.Value, problem.InDate })
            .ToListAsync();

        var metadata = await LoadProblemMetadataAsync(siteId, siteProblems.Select(item => item.ProblemId).ToList());

        var years = metadata.Values
            .SelectMany(item => item.Years)
            .Concat(siteProblems.Where(item => item.InDate.HasValue).Select(item => item.InDate!.Value.Year))
            .Distinct()
            .OrderByDescending(item => item)
            .ToList();

        var tracks = metadata.Values
            .SelectMany(item => item.Tracks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        var tags = metadata.Values
            .SelectMany(item => item.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        var contestSources = metadata.Values
            .Select(item => item.OriginSource)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.First())
            .Where(source =>
                !string.Equals(source, "General", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(source, "OBI", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(source, "ICPC Bolivia", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        return new PublicProblemFiltersResponse
        {
            SiteId = siteId,
            Years = years,
            ContestTracks = tracks,
            ContestSources = contestSources,
            ProblemMenuItems = BuildProblemMenuItems(tracks, contestSources),
            Tags = tags
        };
    }

    public async Task<PublicRankingResponse> GetRankingAsync(int siteId, int limit)
    {
        var rankingRows = await _context.Solutions
            .Where(solution => solution.SiteId == siteId)
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
            UpdatedAtUtc = DateTime.UtcNow,
            Items = items
        };
    }

    public async Task<PublicTopicsResponse> GetTopicsAsync(int siteId)
    {
        var classificationRows = await _context.Classifications
            .Select(classification => new
            {
                classification.ClassificationId,
                classification.TopicId,
                classification.Name,
                ProblemCount = classification.Problems!.Count(problem => problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId))
            })
            .Where(item => item.ProblemCount > 0)
            .ToListAsync();

        var topicIds = classificationRows.Select(item => item.TopicId).Distinct().ToList();
        var topics = await _context.Topics
            .Where(topic => topicIds.Contains(topic.TopicId))
            .ToListAsync();

        var publicTopics = topics
            .Select(topic =>
            {
                var topicClassifications = classificationRows
                    .Where(classification => classification.TopicId == topic.TopicId)
                    .OrderByDescending(classification => classification.ProblemCount)
                    .ThenBy(classification => classification.Name)
                    .Select(classification => new PublicTopicClassification
                    {
                        ClassificationId = classification.ClassificationId,
                        Name = classification.Name,
                        ProblemCount = classification.ProblemCount
                    })
                    .ToList();

                return new PublicTopicItem
                {
                    TopicId = topic.TopicId,
                    Name = topic.Name,
                    ClassificationCount = topicClassifications.Count,
                    ProblemCount = topicClassifications.Sum(item => item.ProblemCount),
                    Classifications = topicClassifications
                };
            })
            .OrderByDescending(topic => topic.ProblemCount)
            .ThenBy(topic => topic.Name)
            .ToList();

        var learningPath = await BuildLearningPathPhasesAsync();

        return new PublicTopicsResponse
        {
            SiteId = siteId,
            UpdatedAtUtc = DateTime.UtcNow,
            Topics = publicTopics,
            LearningPath = learningPath,
            TransversalSkills = new[]
            {
                "Analisis de complejidad",
                "Lectura cuidadosa del enunciado",
                "Debugging sistematico",
                "Manejo de casos borde"
            }
        };
    }

    public async Task<PublicContestsResponse> GetContestsAsync(int siteId, string? status, string? level, string? sortBy, int page, int pageSize, string? searchTerm)
    {
        var updatedAtUtc = DateTime.UtcNow;
        var contestNow = GetContestClockNow();
        var normalizedStatus = (status?.Trim().ToLowerInvariant() ?? "all") switch
        {
            "active" => "ACTIVE",
            "upcoming" => "UPCOMING",
            "past" => "FINISHED",
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
            where contestSite.SiteId == siteId && contest.Defunct == "N"
            select new ContestProjection
            {
                ContestId = contest.ContestId,
                Title = string.IsNullOrWhiteSpace(contest.Title) ? $"Contest #{contest.ContestId}" : contest.Title,
                StartTimeUtc = contest.StartTime,
                EndTimeUtc = contest.EndTime,
                IsPrivate = contest.Private != 0,
                Track = contest.Track,
                Level = contest.Level
            }
        ).ToListAsync();

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
                var computedStatus = ComputeContestStatus(contest.StartTimeUtc, contest.EndTimeUtc, contestNow);
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
                    DurationMinutes = Math.Max(1, (int)Math.Round((contest.EndTimeUtc - contest.StartTimeUtc).TotalMinutes)),
                    ProblemCount = problemCounts.TryGetValue(contest.ContestId, out var problemCount) ? problemCount : 0,
                    ParticipantCount = participantCounts.TryGetValue(contest.ContestId, out var participantCount) ? participantCount : 0
                };
            })
            .Where(item =>
            {
                if (normalizedStatus != "ALL" && item.Status != normalizedStatus)
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
        var contest = await EnsureContestExistsAsync(siteId, contestId);
        var generatedAtUtc = DateTime.UtcNow;
        var contestNow = GetContestClockNow();

        var problemCount = await _context.ContestProblems
            .Where(item => item.ContestId == contestId)
            .CountAsync();

        var participantIds = await _context.ContestUsers
            .Where(item => item.SiteId == siteId && item.ContestId == contestId)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync();

        var submissionStats = await _context.Solutions
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

        var userIds = participantIds
            .Union(submissionStats.Select(item => item.UserId))
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
            StartTimeUtc = contest.StartTime,
            EndTimeUtc = contest.EndTime,
            Status = ComputeContestStatus(contest.StartTime, contest.EndTime, contestNow),
            Track = contestTrack,
            Level = contestLevel,
            SiteId = siteId,
            GeneratedAtUtc = generatedAtUtc,
            DurationMinutes = Math.Max(1, (int)Math.Round((contest.EndTime - contest.StartTime).TotalMinutes)),
            IsPrivate = contest.Private != 0,
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
        await EnsureContestExistsAsync(siteId, contestId);

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

    public async Task<PublicSubmissionsResponse> GetSubmissionsAsync(int siteId, int page, int pageSize, int? contestId)
    {
        var baseQuery = _context.Solutions
            .Where(solution => solution.SiteId == siteId);

        if (contestId.HasValue)
        {
            await EnsureContestExistsAsync(siteId, contestId.Value);
            baseQuery = baseQuery.Where(solution => solution.ContestId == contestId.Value);
        }

        var total = await baseQuery.CountAsync();

        var solutions = await baseQuery
            .OrderByDescending(solution => solution.SolutionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = siteId,
                Total = total,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.UtcNow,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var userIds = solutions.Select(solution => solution.UserId).Distinct().ToList();
        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var nickMap = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => string.IsNullOrWhiteSpace(profile.Nick) ? profile.UserId : profile.Nick);

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        var items = solutions
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionListItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ContestId = solution.ContestId,
                    ContestProblemId = solution.ContestId.HasValue && solution.Num >= 0 ? ContestProblemCode.FromNumber(solution.Num) : null,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    UserId = solution.UserId,
                    Nick = nickMap.GetValueOrDefault(solution.UserId, solution.UserId),
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
                    ResultCode = solution.Result,
                    StatusKey = verdict.StatusKey,
                    StatusLabel = verdict.StatusLabel,
                    GeneralStatusKey = verdict.GeneralStatusKey,
                    GeneralStatusLabel = verdict.GeneralStatusLabel,
                    IsFinal = verdict.IsFinal,
                    TimeMs = solution.Time,
                    MemoryKb = solution.Memory,
                    PassRate = solution.PassRate,
                    CreatedAtUtc = solution.InDate,
                    JudgeTimeUtc = solution.Judgetime
                };
            })
            .ToList();

        return new PublicSubmissionsResponse
        {
            SiteId = siteId,
            Total = total,
            Page = page,
            PageSize = pageSize,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = items
        };
    }

    public async Task<PublicSubmissionsResponse> GetOwnSubmissionsAsync(CurrentUser currentUser, int page, int pageSize)
    {
        var baseQuery = _context.Solutions
            .Where(solution => solution.SiteId == currentUser.SiteId && solution.UserId == currentUser.UserId);

        var total = await baseQuery.CountAsync();

        var solutions = await baseQuery
            .OrderByDescending(solution => solution.SolutionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = currentUser.SiteId,
                Total = total,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.UtcNow,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var nick = await _context.UserProfiles
            .Where(profile => profile.SiteId == currentUser.SiteId && profile.UserId == currentUser.UserId)
            .Select(profile => string.IsNullOrWhiteSpace(profile.Nick) ? currentUser.UserId : profile.Nick)
            .FirstOrDefaultAsync() ?? currentUser.UserId;

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        var items = solutions
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionListItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ContestId = solution.ContestId,
                    ContestProblemId = solution.ContestId.HasValue && solution.Num >= 0 ? ContestProblemCode.FromNumber(solution.Num) : null,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    UserId = solution.UserId,
                    Nick = nick,
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
                    ResultCode = solution.Result,
                    StatusKey = verdict.StatusKey,
                    StatusLabel = verdict.StatusLabel,
                    GeneralStatusKey = verdict.GeneralStatusKey,
                    GeneralStatusLabel = verdict.GeneralStatusLabel,
                    IsFinal = verdict.IsFinal,
                    TimeMs = solution.Time,
                    MemoryKb = solution.Memory,
                    PassRate = solution.PassRate,
                    CreatedAtUtc = solution.InDate,
                    JudgeTimeUtc = solution.Judgetime
                };
            })
            .ToList();

        return new PublicSubmissionsResponse
        {
            SiteId = currentUser.SiteId,
            Total = total,
            Page = page,
            PageSize = pageSize,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = items
        };
    }

    public async Task<IReadOnlyCollection<PublicSubmissionSourceCodeItem>> GetOwnSubmissionSourceCodesAsync(CurrentUser currentUser)
    {
        var solutions = await _context.Solutions
            .AsNoTracking()
            .Where(solution => solution.SiteId == currentUser.SiteId && solution.UserId == currentUser.UserId)
            .Include(solution => solution.SourceCode)
            .OrderByDescending(solution => solution.SolutionId)
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return Array.Empty<PublicSubmissionSourceCodeItem>();
        }

        var problemIds = solutions.Select(solution => solution.ProblemId).Distinct().ToList();
        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();

        var problemTitleMap = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
            .ToDictionaryAsync(
                problem => problem.ProblemId!.Value,
                problem => string.IsNullOrWhiteSpace(problem.Title) ? $"Problema #{problem.ProblemId}" : problem.Title);

        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        return solutions
            .Where(solution => !string.IsNullOrWhiteSpace(solution.SourceCode?.Source))
            .Select(solution =>
            {
                var verdict = JudgeVerdictCatalog.Map(solution.Result);
                var languageId = (int)solution.Language;

                return new PublicSubmissionSourceCodeItem
                {
                    SolutionId = solution.SolutionId,
                    ProblemId = solution.ProblemId,
                    ProblemTitle = problemTitleMap.GetValueOrDefault(solution.ProblemId, $"Problema #{solution.ProblemId}"),
                    LanguageId = languageId,
                    LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
                    StatusKey = verdict.StatusKey,
                    CreatedAtUtc = solution.InDate,
                    SourceCode = solution.SourceCode!.Source
                };
            })
            .ToList();
    }

    public async Task<PublicSubmissionResponse> SubmitAsync(CurrentUser currentUser, PublicSubmissionRequest request, int languageId)
    {
        var userExists = await _context.Users
            .AnyAsync(user => user.UserId == currentUser.UserId
                && user.SiteId == currentUser.SiteId
                && !user.IsDeleted
                && user.IsActive);

        if (!userExists)
        {
            throw new ArgumentException("Usuario inválido para este sitio.");
        }

        var resolvedProblemId = request.ProblemId;
        var contestNum = -1;

        if (request.ContestId.HasValue && !string.IsNullOrWhiteSpace(request.ContestProblemId))
        {
            var contestProblem = await ResolveContestProblemReferenceAsync(currentUser.SiteId, request.ContestId.Value, request.ContestProblemId);
            resolvedProblemId = contestProblem.ProblemId;
            contestNum = contestProblem.Num;
        }

        if (!resolvedProblemId.HasValue || resolvedProblemId.Value <= 0)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        var problemExists = await _context.ProblemSites
            .AnyAsync(problemSite => problemSite.problemId == resolvedProblemId.Value && problemSite.SiteId == currentUser.SiteId);

        if (!problemExists)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        var languageExists = await _context.ProgrammingLanguages
            .AnyAsync(language => language.LanguageId == languageId);

        if (!languageExists)
        {
            throw new ArgumentException("LanguageId no soportado.");
        }

        if (request.ContestId.HasValue && request.ContestId.Value > 0)
        {
            var contest = await EnsureContestExistsAsync(currentUser.SiteId, request.ContestId.Value);
            EnsureContestAcceptsSubmissions(contest);
        }

        if (contestNum < 0 && request.ContestId.HasValue && request.ContestId.Value > 0)
        {
            contestNum = await _context.ContestProblems
                .Where(item => item.ContestId == request.ContestId.Value && item.ProblemId == resolvedProblemId.Value)
                .Select(item => (int?)item.Num)
                .FirstOrDefaultAsync() ?? -1;
        }

        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var solution = new DbSolution
        {
            ProblemId = resolvedProblemId.Value,
            UserId = currentUser.UserId,
            Time = 0,
            Memory = 0,
            InDate = now,
            Result = 0,
            Language = (uint)languageId,
            Ip = "0.0.0.0",
            ContestId = request.ContestId,
            Num = (sbyte)Math.Clamp(contestNum, sbyte.MinValue, sbyte.MaxValue),
            CodeLength = request.SourceCode.Length,
            PassRate = 0,
            IsRemoteOj = false,
            RemoteId = 0,
            SiteId = currentUser.SiteId
        };

        await _context.Solutions.AddAsync(solution);
        await _context.SaveChangesAsync();

        await _context.SourceCodes.AddAsync(new DbSourceCode
        {
            SolutionId = solution.SolutionId,
            Source = request.SourceCode
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new PublicSubmissionResponse
        {
            SolutionId = solution.SolutionId,
            LanguageId = languageId,
            AutoDetected = false,
            CreatedAtUtc = now
        };
    }

    public async Task<PublicAuthenticatedUser> LoginAsync(string userOrEmail, string password, int siteId)
    {
        var normalizedUserOrEmail = userOrEmail.Trim();
        var normalizedEmail = normalizedUserOrEmail.ToLowerInvariant();

        var user = await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            where item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
                && (item.UserId == normalizedUserOrEmail
                    || (!string.IsNullOrWhiteSpace(profile.Email) && profile.Email!.ToLower() == normalizedEmail))
            select new
            {
                item.UserId,
                item.Password,
                Nick = profile != null ? profile.Nick : item.UserId,
                LastName = profile != null ? profile.Lastname : string.Empty,
                Email = profile != null ? profile.Email : string.Empty
            }
        ).FirstOrDefaultAsync();

        if (user == null || !LegacyPasswordHasher.Verify(password, user.Password))
        {
            throw new UnauthorizedAccessException("Usuario o contraseña incorrectos.");
        }

        var dbUser = await _context.Users.FirstAsync(item => item.UserId == user.UserId && item.SiteId == siteId);
        dbUser.Accesstime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new PublicAuthenticatedUser
        {
            UserId = user.UserId,
            Nick = string.IsNullOrWhiteSpace(user.Nick) ? user.UserId : user.Nick,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = await ResolveUserRoleAsync(user.UserId, siteId),
            SiteId = siteId
        };
    }

    public async Task<PublicAuthenticatedUser> RegisterAsync(string userId, string passwordHash, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress)
    {
        var emailLower = email.ToLowerInvariant();

        var userExists = await _context.Users
            .AnyAsync(user => user.UserId == userId);

        if (userExists)
        {
            throw new InvalidOperationException("El usuario ya existe.");
        }

        var emailExists = await _context.UserProfiles
            .AnyAsync(profile => !string.IsNullOrWhiteSpace(profile.Email) && profile.Email!.ToLower() == emailLower);

        if (emailExists)
        {
            throw new InvalidOperationException("El correo electrónico ya está registrado.");
        }

        var now = DateTime.UtcNow;
        var role = nameof(UserRolesEnum.Invitado);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.Users.AddAsync(new DbUser
        {
            UserId = userId,
            Password = passwordHash,
            Ip = ipAddress,
            Accesstime = now,
            SiteId = siteId,
            RegTime = now,
            IsActive = true,
            IsDeleted = false
        });

        await _context.UserProfiles.AddAsync(new DbUserProfile
        {
            UserId = userId,
            SiteId = siteId,
            Email = email,
            Nick = nick ?? userId,
            School = school,
            Lastname = lastName,
            Departament = 0
        });

        await _context.UserActivities.AddAsync(new DbUserActivity
        {
            UserId = userId,
            SiteId = siteId,
            Submit = 0,
            Solved = 0
        });

        var guestRole = await _context.Roles
            .Where(roleItem => roleItem.RoleName.ToLower() == "invitado" || roleItem.RoleName.ToLower() == "guest")
            .OrderBy(roleItem => roleItem.RoleId)
            .Select(roleItem => new
            {
                roleItem.RoleId,
                roleItem.RoleName
            })
            .FirstOrDefaultAsync();

        if (guestRole != null)
        {
            if (string.IsNullOrWhiteSpace(guestRole.RoleName))
            {
                role = nameof(UserRolesEnum.Invitado);
            }
            else
            {
                var roleName = guestRole.RoleName.Trim().ToLowerInvariant();
                role = roleName.Contains("admin")
                    ? nameof(UserRolesEnum.Administrador)
                    : roleName.Contains("aux")
                        ? nameof(UserRolesEnum.Auxiliar)
                        : roleName.Contains("doc")
                            ? nameof(UserRolesEnum.Docente)
                            : nameof(UserRolesEnum.Invitado);
            }

            await _context.UserRoles.AddAsync(new DbUserRole
            {
                UserId = userId,
                RoleId = guestRole.RoleId,
                SiteId = siteId
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new PublicAuthenticatedUser
        {
            UserId = userId,
            Nick = nick ?? userId,
            LastName = lastName ?? string.Empty,
            Email = email,
            Role = role,
            SiteId = siteId
        };
    }

    public async Task<PublicPasswordRecoveryTarget?> GetPasswordRecoveryTargetAsync(string email, int siteId)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId
            where item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
                && !string.IsNullOrWhiteSpace(profile.Email)
                && profile.Email!.ToLower() == normalizedEmail
            select new PublicPasswordRecoveryTarget
            {
                UserId = item.UserId,
                Email = profile.Email!,
                Nick = string.IsNullOrWhiteSpace(profile.Nick) ? item.UserId : profile.Nick!
            }
        ).FirstOrDefaultAsync();
    }

    public async Task SavePasswordRecoveryTokenAsync(string userId, int siteId, string tokenHash, DateTime expiresAtUtc)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item =>
            item.UserId == userId
            && item.SiteId == siteId
            && !item.IsDeleted
            && item.IsActive);

        if (user == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        user.ResetPasswordToken = tokenHash;
        user.ResetPasswordExpires = expiresAtUtc;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string email, int siteId, string tokenHash, DateTime nowUtc, string passwordHash)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId
            where item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
                && !string.IsNullOrWhiteSpace(profile.Email)
                && profile.Email!.ToLower() == normalizedEmail
                && item.ResetPasswordToken == tokenHash
                && item.ResetPasswordExpires.HasValue
                && item.ResetPasswordExpires.Value >= nowUtc
            select item
        ).FirstOrDefaultAsync();

        if (user == null)
        {
            return false;
        }

        user.Password = passwordHash;
        user.ResetPasswordToken = null;
        user.ResetPasswordExpires = null;
        user.Accesstime = nowUtc;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PublicAuthenticatedUser> GetAuthenticatedUserAsync(string userId, int siteId)
    {
        var user = await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            where item.UserId == userId
                && item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
            select new
            {
                item.UserId,
                Nick = profile != null ? profile.Nick : item.UserId,
                LastName = profile != null ? profile.Lastname : string.Empty,
                Email = profile != null ? profile.Email : string.Empty
            }
        ).FirstOrDefaultAsync();

        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no autorizado.");
        }

        return new PublicAuthenticatedUser
        {
            UserId = user.UserId,
            Nick = string.IsNullOrWhiteSpace(user.Nick) ? user.UserId : user.Nick,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = await ResolveUserRoleAsync(user.UserId, siteId),
            SiteId = siteId
        };
    }

    private async Task<Dictionary<int, ProblemMetadata>> LoadProblemMetadataAsync(int siteId, IEnumerable<int> problemIds)
    {
        var problemIdList = problemIds
            .Distinct()
            .ToList();

        var metadata = problemIdList
            .ToDictionary(problemId => problemId, _ => new ProblemMetadata());

        if (metadata.Count == 0)
        {
            return metadata;
        }

        var taggedProblems = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue && problemIdList.Contains(problem.ProblemId.Value))
            .Include(problem => problem.Classifications)
            .ToListAsync();

        foreach (var problem in taggedProblems)
        {
            var problemId = problem.ProblemId!.Value;
            foreach (var classification in problem.Classifications ?? Array.Empty<DbClassification>())
            {
                metadata[problemId].Tags.Add(classification.Name);
            }

            var normalizedOriginSource = CollapseWhitespace(problem.OriginSource);
            if (!string.IsNullOrWhiteSpace(normalizedOriginSource))
            {
                metadata[problemId].OriginSource = normalizedOriginSource;
                metadata[problemId].Sources.Add(normalizedOriginSource);
                metadata[problemId].Tracks.Add(
                    normalizedOriginSource.Contains("OBI", StringComparison.OrdinalIgnoreCase)
                        ? "OBI"
                        : normalizedOriginSource.Contains("ICPC", StringComparison.OrdinalIgnoreCase)
                            || normalizedOriginSource.Contains("Bolivia", StringComparison.OrdinalIgnoreCase)
                            ? "ICPC_BOLIVIA"
                            : "GENERAL");
            }
        }

        var contests = await (
            from contestProblem in _context.ContestProblems
            join contest in _context.Contests on contestProblem.ContestId equals contest.ContestId
            join contestSite in _context.ContestSites on contest.ContestId equals contestSite.ContestId
            where contestProblem.ProblemId.HasValue
                && contestSite.SiteId == siteId
                && problemIdList.Contains(contestProblem.ProblemId.Value)
                && contest.Defunct == "N"
            select new
            {
                ProblemId = contestProblem.ProblemId!.Value,
                contest.ContestId,
                contest.StartTime,
                contest.Title,
                contest.Track
            }
        )
        .OrderBy(item => item.ProblemId)
        .ThenBy(item => item.StartTime == DateTime.MinValue)
        .ThenBy(item => item.StartTime)
        .ThenBy(item => item.ContestId)
        .ToListAsync();

        foreach (var item in contests
            .GroupBy(item => item.ProblemId)
            .Select(group => group.First()))
        {
            if (item.StartTime != DateTime.MinValue && item.StartTime.Year > 0)
            {
                metadata[item.ProblemId].Years.Add(item.StartTime.Year);
            }

            if (string.IsNullOrWhiteSpace(metadata[item.ProblemId].OriginSource))
            {
                var cleanedTitle = CollapseWhitespace(Regex.Replace(WebUtility.HtmlDecode(item.Title ?? string.Empty), "<.*?>", " "));
                var originYear = item.StartTime != DateTime.MinValue && item.StartTime.Year > 0
                    ? item.StartTime.Year
                    : (int?)null;
                metadata[item.ProblemId].OriginSource = !string.IsNullOrWhiteSpace(cleanedTitle)
                    ? cleanedTitle
                    : originYear.HasValue
                        ? $"General {originYear.Value}"
                        : "General";
                metadata[item.ProblemId].Sources.Add(metadata[item.ProblemId].OriginSource);
            }

            if (metadata[item.ProblemId].Tracks.Count == 0)
            {
                metadata[item.ProblemId].Tracks.Add(string.IsNullOrWhiteSpace(item.Track) ? "GENERAL" : item.Track);
            }
        }

        foreach (var problem in taggedProblems)
        {
            var problemId = problem.ProblemId!.Value;
            if (metadata[problemId].Years.Count == 0
                && problem.InDate.HasValue
                && problem.InDate.Value != DateTime.MinValue
                && problem.InDate.Value.Year > 0)
            {
                metadata[problemId].Years.Add(problem.InDate.Value.Year);
            }

            if (string.IsNullOrWhiteSpace(metadata[problemId].OriginSource))
            {
                metadata[problemId].OriginSource = problem.InDate.HasValue
                    && problem.InDate.Value != DateTime.MinValue
                    && problem.InDate.Value.Year > 0
                    ? $"General {problem.InDate.Value.Year}"
                    : "General";
                metadata[problemId].Sources.Add(metadata[problemId].OriginSource);
            }

            if (metadata[problemId].Tracks.Count == 0)
            {
                metadata[problemId].Tracks.Add(
                    metadata[problemId].OriginSource.Contains("OBI", StringComparison.OrdinalIgnoreCase)
                        ? "OBI"
                        : metadata[problemId].OriginSource.Contains("ICPC", StringComparison.OrdinalIgnoreCase)
                            || metadata[problemId].OriginSource.Contains("Bolivia", StringComparison.OrdinalIgnoreCase)
                            ? "ICPC_BOLIVIA"
                            : "GENERAL");
            }
        }

        return metadata;
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

    private async Task<ContestProblemReference> ResolveContestProblemReferenceAsync(int siteId, int contestId, string contestProblemId)
    {
        await EnsureContestExistsAsync(siteId, contestId);

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
                && problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId)
                && (problem.Defunct == "N" || problem.Defunct == "Y")
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

    private async Task<HashSet<int>> LoadSolvedProblemIdsAsync(int siteId, CurrentUser? currentUser, IEnumerable<int> problemIds, int? contestId = null)
    {
        var problemIdList = problemIds.Distinct().ToList();
        if (currentUser == null
            || string.IsNullOrWhiteSpace(currentUser.UserId)
            || currentUser.UserId == "defaultUserId"
            || problemIdList.Count == 0)
        {
            return new HashSet<int>();
        }

        var solvedProblemIds = await _context.Solutions
            .Where(solution =>
                solution.SiteId == siteId
                && solution.Result == AcceptedResultCode
                && solution.UserId == currentUser.UserId
                && (!contestId.HasValue || solution.ContestId == contestId.Value)
                && problemIdList.Contains(solution.ProblemId))
            .Select(solution => solution.ProblemId)
            .Distinct()
            .ToListAsync();

        return solvedProblemIds.ToHashSet();
    }

    private async Task<HashSet<int>> LoadAttemptedProblemIdsAsync(int siteId, CurrentUser? currentUser, IEnumerable<int> problemIds, int? contestId = null)
    {
        var problemIdList = problemIds.Distinct().ToList();
        if (currentUser == null
            || string.IsNullOrWhiteSpace(currentUser.UserId)
            || currentUser.UserId == "defaultUserId"
            || problemIdList.Count == 0)
        {
            return new HashSet<int>();
        }

        var attemptedProblemIds = await _context.Solutions
            .Where(solution =>
                solution.SiteId == siteId
                && solution.UserId == currentUser.UserId
                && (!contestId.HasValue || solution.ContestId == contestId.Value)
                && problemIdList.Contains(solution.ProblemId))
            .Select(solution => solution.ProblemId)
            .Distinct()
            .ToListAsync();

        return attemptedProblemIds.ToHashSet();
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

    private async Task<string> ResolveUserRoleAsync(string userId, int siteId)
    {
        var roleName = await (
            from userRole in _context.UserRoles
            join role in _context.Roles on userRole.RoleId equals role.RoleId
            where userRole.UserId == userId && userRole.SiteId == siteId
            orderby role.RoleId
            select role.RoleName
        ).FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return nameof(UserRolesEnum.Invitado);
        }

        var value = roleName.Trim().ToLowerInvariant();
        if (value.Contains("admin"))
        {
            return nameof(UserRolesEnum.Administrador);
        }

        if (value.Contains("aux"))
        {
            return nameof(UserRolesEnum.Auxiliar);
        }

        if (value.Contains("doc"))
        {
            return nameof(UserRolesEnum.Docente);
        }

        return nameof(UserRolesEnum.Invitado);
    }

    private async Task<DbContest> EnsureContestExistsAsync(int siteId, int contestId)
    {
        var contest = await (
            from contestSite in _context.ContestSites
            join item in _context.Contests on contestSite.ContestId equals item.ContestId
            where contestSite.SiteId == siteId && item.ContestId == contestId && item.Defunct == "N"
            select item
        ).FirstOrDefaultAsync();

        if (contest == null)
        {
            throw new ArgumentException("Concurso no encontrado.");
        }

        return contest;
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

    private static decimal CalculateSuccessRate(int accepted, int submit)
    {
        return submit <= 0 ? 0 : Math.Round((decimal)accepted * 100m / submit, 2);
    }

    private static string CalculateDifficulty(int accepted, int submit)
    {
        var successRate = CalculateSuccessRate(accepted, submit);
        if (successRate >= 65m)
        {
            return "EASY";
        }

        if (successRate >= 35m)
        {
            return "MEDIUM";
        }

        return "HARD";
    }

    private static string ComputeContestStatus(DateTime startTimeUtc, DateTime endTimeUtc, DateTime nowUtc)
    {
        if (startTimeUtc > nowUtc)
        {
            return "UPCOMING";
        }

        if (endTimeUtc >= nowUtc)
        {
            return "ACTIVE";
        }

        return "FINISHED";
    }

    private static void EnsureContestAcceptsSubmissions(DbContest contest)
    {
        var now = GetContestClockNow();

        if (contest.StartTime > now)
        {
            throw new InvalidOperationException("Este concurso todavía no acepta envíos.");
        }

        if (contest.EndTime < now)
        {
            throw new InvalidOperationException("Este concurso ya finalizó y no acepta envíos.");
        }
    }

    private static DateTime GetContestClockNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ContestTimeZone);
    }

    private static TimeZoneInfo ResolveContestTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/La_Paz", "SA Western Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }

    private static IReadOnlyCollection<PublicProblemMenuItem> BuildProblemMenuItems(
        IEnumerable<string> contestTracks,
        IEnumerable<string> contestSources)
    {
        var items = new List<PublicProblemMenuItem>();
        var normalizedTracks = new HashSet<string>(contestTracks, StringComparer.OrdinalIgnoreCase);

        if (normalizedTracks.Contains("GENERAL"))
        {
            items.Add(new PublicProblemMenuItem
            {
                Key = "general",
                Label = "General",
                ContestTrack = "general"
            });
        }

        if (normalizedTracks.Contains("OBI"))
        {
            items.Add(new PublicProblemMenuItem
            {
                Key = "obi",
                Label = "OBI",
                ContestTrack = "obi"
            });
        }

        if (normalizedTracks.Contains("ICPC_BOLIVIA"))
        {
            items.Add(new PublicProblemMenuItem
            {
                Key = "icpc_bolivia",
                Label = "ICPC Bolivia",
                ContestTrack = "icpc_bolivia"
            });
        }

        foreach (var source in contestSources)
        {
            if (string.IsNullOrWhiteSpace(source)
                || string.Equals(source, "General", StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, "OBI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, "ICPC Bolivia", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new PublicProblemMenuItem
            {
                Key = $"source:{source.Trim()}",
                Label = source.Trim(),
                ContestTrack = "all",
                Source = source.Trim()
            });
        }

        return items;
    }

    private static int GetProblemUserStatusSortRank(int problemId, HashSet<int>? solvedProblemIds, HashSet<int>? attemptedProblemIds)
    {
        if (solvedProblemIds?.Contains(problemId) == true)
        {
            return 2;
        }

        if (attemptedProblemIds?.Contains(problemId) == true)
        {
            return 1;
        }

        return 0;
    }

    private static string CollapseWhitespace(string? originSource)
    {
        if (string.IsNullOrWhiteSpace(originSource))
        {
            return string.Empty;
        }

        return Regex.Replace(originSource.Trim(), @"\s+", " ");
    }

    private sealed class ProblemMetadata
    {
        public static ProblemMetadata Empty { get; } = new();

        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<int> Years { get; } = new();

        public HashSet<string> Tracks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string OriginSource { get; set; } = string.Empty;
    }

    private sealed class ProblemListProjection
    {
        public int ProblemId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int Accepted { get; set; }

        public int Submit { get; set; }

        public int Solved { get; set; }

        public DateTime? PublishedAt { get; set; }
    }

    private sealed class ContestProjection
    {
        public int ContestId { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime StartTimeUtc { get; set; }

        public DateTime EndTimeUtc { get; set; }

        public bool IsPrivate { get; set; }

        public string Track { get; set; } = "GENERAL";

        public string Level { get; set; } = "PRACTICE";
    }

    private sealed class ContestProblemReference
    {
        public int ContestId { get; set; }

        public int ProblemId { get; set; }

        public int Num { get; set; }

        public string ContestProblemId { get; set; } = string.Empty;
    }
}
