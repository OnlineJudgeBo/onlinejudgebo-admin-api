using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class PublicRepository
{
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
            var contest = await GetContestAsync(siteId, contestId.Value);
            await CheckContestAccessAsync(siteId, contest, currentUser);
        }

        var contestProblemMap = contestId.HasValue
            ? await LoadContestProblemMapAsync(contestId.Value)
            : null;

        var problems = await _context.Problems
            .Where(problem => problem.ProblemId.HasValue
                && problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive)
                && (!contestId.HasValue || _context.ContestProblems.Any(contestProblem =>
                    contestProblem.ContestId == contestId.Value && contestProblem.ProblemId == problem.ProblemId))
                && (problem.Defunct == "N" || problem.Defunct == "Y" || problem.Defunct == "O"))
            .Select(problem => new ProblemListProjection
            {
                ProblemId = problem.ProblemId!.Value,
                Title = problem.Title,
                Description = problem.Description ?? string.Empty,
                Source = problem.Source ?? string.Empty,
                Accepted = problem.Accepted ?? 0,
                Submit = problem.Submit ?? 0,
                Solved = problem.Solved ?? 0,
                PublishedAt = problem.InDate
            })
            .ToListAsync();

        if (contestId.HasValue && problems.Count > 0)
        {
            var contestProblemIds = problems.Select(problem => problem.ProblemId).Distinct().ToList();
            var contestProblemStats = await OfficialSolutions()
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
                    Source = problem.Source,
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
            .Include(item => item.SampleCases)
            .FirstOrDefaultAsync(item => item.ProblemId == problemId
                && item.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive)
                && (item.Defunct == "N" || item.Defunct == "Y" || item.Defunct == "O"));

        if (problem == null)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        return await BuildProblemDetailResponseAsync(siteId, problem, problemId);
    }

    public async Task<PublicProblemDetailResponse> GetContestProblemDetailAsync(int siteId, int contestId, string contestProblemId, CurrentUser? currentUser = null)
    {
        var contestProblem = await ResolveContestProblemReferenceAsync(siteId, contestId, contestProblemId, currentUser);
        var problem = await _context.Problems
            .Include(item => item.SampleCases)
            .FirstOrDefaultAsync(item => item.ProblemId == contestProblem.ProblemId
                && item.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive)
                && (item.Defunct == "N" || item.Defunct == "Y" || item.Defunct == "O"));

        if (problem == null)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        return await BuildProblemDetailResponseAsync(siteId, problem, contestProblem.ProblemId, contestId, contestProblem.ContestProblemId);
    }

    public async Task<PublicProblemStatisticsResponse> GetProblemStatisticsAsync(int siteId, int problemId)
    {
        var problem = await _context.Problems
            .Where(item => item.ProblemId == problemId
                && item.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive))
            .Select(item => new
            {
                ProblemId = item.ProblemId!.Value,
                Title = string.IsNullOrWhiteSpace(item.Title) ? $"Problema #{item.ProblemId}" : item.Title
            })
            .FirstOrDefaultAsync();

        if (problem == null)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        var solutions = await OfficialSolutions()
            .Where(solution => solution.SiteId == siteId && solution.ProblemId == problemId)
            .ToListAsync();

        var acceptedSolutions = solutions
            .Where(solution => solution.Result == AcceptedResultCode)
            .OrderBy(solution => solution.InDate)
            .ThenBy(solution => solution.SolutionId)
            .ToList();

        var languageIds = solutions.Select(solution => (int)solution.Language).Distinct().ToList();
        var languageNameMap = await _context.ProgrammingLanguages
            .Where(language => language.LanguageId.HasValue && languageIds.Contains(language.LanguageId.Value))
            .ToDictionaryAsync(
                language => language.LanguageId!.Value,
                language => string.IsNullOrWhiteSpace(language.Name) ? $"Lenguaje #{language.LanguageId}" : language.Name!);

        var userIds = acceptedSolutions
            .Select(solution => solution.UserId)
            .Distinct()
            .ToList();
        var nickMap = await _context.UserProfiles
            .Where(profile => profile.SiteId == siteId && userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => string.IsNullOrWhiteSpace(profile.Nick) ? profile.UserId : profile.Nick);

        var bestTime = acceptedSolutions
            .Where(solution => solution.Time > 0)
            .OrderBy(solution => solution.Time)
            .ThenBy(solution => solution.Memory)
            .ThenBy(solution => solution.InDate)
            .FirstOrDefault();
        var bestMemory = acceptedSolutions
            .Where(solution => solution.Memory > 0)
            .OrderBy(solution => solution.Memory)
            .ThenBy(solution => solution.Time)
            .ThenBy(solution => solution.InDate)
            .FirstOrDefault();

        return new PublicProblemStatisticsResponse
        {
            SiteId = siteId,
            ProblemId = problem.ProblemId,
            Title = problem.Title,
            TotalSubmissions = solutions.Count,
            Accepted = solutions.Count(solution => solution.Result == JudgeResultCodes.Accepted),
            WrongAnswer = solutions.Count(solution => solution.Result == JudgeResultCodes.WrongAnswer),
            TimeLimitExceeded = solutions.Count(solution => solution.Result == JudgeResultCodes.TimeLimitExceeded),
            MemoryLimitExceeded = solutions.Count(solution => solution.Result == JudgeResultCodes.MemoryLimitExceeded),
            CompileError = solutions.Count(solution => solution.Result == JudgeResultCodes.CompileError),
            RuntimeError = solutions.Count(solution => solution.Result == JudgeResultCodes.RuntimeError),
            OtherResults = solutions.Count(solution => solution.Result is not (
                JudgeResultCodes.Accepted
                or JudgeResultCodes.WrongAnswer
                or JudgeResultCodes.TimeLimitExceeded
                or JudgeResultCodes.MemoryLimitExceeded
                or JudgeResultCodes.CompileError
                or JudgeResultCodes.RuntimeError)),
            AcceptanceRate = solutions.Count == 0 ? 0 : Math.Round((decimal)acceptedSolutions.Count * 100m / solutions.Count, 2),
            Languages = solutions
                .GroupBy(solution => (int)solution.Language)
                .Select(group => new ProblemStatisticsLanguageItem
                {
                    LanguageId = group.Key,
                    LanguageName = languageNameMap.GetValueOrDefault(group.Key, $"Lenguaje #{group.Key}"),
                    Submissions = group.Count(),
                    Accepted = group.Count(solution => solution.Result == AcceptedResultCode)
                })
                .OrderByDescending(item => item.Submissions)
                .ThenBy(item => item.LanguageId)
                .ToList(),
            BestTime = bestTime == null ? null : ToProblemStatisticsBestRun(bestTime, languageNameMap, nickMap),
            BestMemory = bestMemory == null ? null : ToProblemStatisticsBestRun(bestMemory, languageNameMap, nickMap),
            FirstAccepted = acceptedSolutions
                .Take(10)
                .Select(solution => ToProblemStatisticsAcceptedItem(solution, languageNameMap, nickMap))
                .ToList()
        };
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

        var samples = problem.SampleCases
            .OrderBy(sample => sample.Num)
            .Select(sample => new ProblemSampleCase
            {
                Index = sample.Num,
                Input = sample.Input ?? string.Empty,
                Output = sample.Output ?? string.Empty
            })
            .ToList();

        // Problemas antiguos sólo tienen el par plano sample_input/sample_output;
        // problem_sample_case queda vacía hasta que se re-editan con la lista nueva.
        if (samples.Count == 0 && (!string.IsNullOrWhiteSpace(problem.SampleInput) || !string.IsNullOrWhiteSpace(problem.SampleOutput)))
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
            TimeLimitSeconds = Math.Round((decimal)problem.TimeLimit, 2),
            MemoryLimitMb = Math.Max(1, problem.MemoryLimit),
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
            .Where(problem => problem.ProblemId.HasValue && problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive))
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
            .Select(item => NormalizeContestSourceForFilters(item.OriginSource))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.First())
            .Where(source => !IsBuiltInProblemMenuSource(source))
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

    public async Task<PublicTopicsResponse> GetTopicsAsync(int siteId)
    {
        var classificationRows = await _context.Classifications
            .Select(classification => new
            {
                classification.ClassificationId,
                classification.TopicId,
                classification.Name,
                ProblemCount = classification.Problems!.Count(problem => problem.ProblemSites!.Any(problemSite => problemSite.SiteId == siteId && problemSite.IsActive))
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
            UpdatedAtUtc = DateTime.Now,
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
                metadata[item.ProblemId].OriginSource = "General";
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
                metadata[problemId].OriginSource = "General";
                metadata[problemId].Sources.Add(metadata[problemId].OriginSource);
            }

            // No contest carries a real Track for this problem (see the contest join
            // above) - default to GENERAL instead of guessing one from OriginSource text.
            if (metadata[problemId].Tracks.Count == 0)
            {
                metadata[problemId].Tracks.Add("GENERAL");
            }
        }

        return metadata;
    }

    private static ProblemStatisticsBestRun ToProblemStatisticsBestRun(
        DbSolution solution,
        IReadOnlyDictionary<int, string> languageNameMap,
        IReadOnlyDictionary<string, string> nickMap)
    {
        var languageId = (int)solution.Language;

        return new ProblemStatisticsBestRun
        {
            SolutionId = solution.SolutionId,
            UserId = solution.UserId,
            Nick = nickMap.GetValueOrDefault(solution.UserId, solution.UserId),
            LanguageId = languageId,
            LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
            TimeMs = solution.Time,
            MemoryKb = solution.Memory,
            CreatedAtUtc = solution.InDate
        };
    }

    private static ProblemStatisticsAcceptedItem ToProblemStatisticsAcceptedItem(
        DbSolution solution,
        IReadOnlyDictionary<int, string> languageNameMap,
        IReadOnlyDictionary<string, string> nickMap)
    {
        var languageId = (int)solution.Language;

        return new ProblemStatisticsAcceptedItem
        {
            SolutionId = solution.SolutionId,
            UserId = solution.UserId,
            Nick = nickMap.GetValueOrDefault(solution.UserId, solution.UserId),
            LanguageId = languageId,
            LanguageName = languageNameMap.GetValueOrDefault(languageId, $"Lenguaje #{languageId}"),
            TimeMs = solution.Time,
            MemoryKb = solution.Memory,
            CreatedAtUtc = solution.InDate
        };
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
            var normalizedSource = NormalizeContestSourceForFilters(source);

            if (string.IsNullOrWhiteSpace(normalizedSource)
                || IsBuiltInProblemMenuSource(normalizedSource))
            {
                continue;
            }

            items.Add(new PublicProblemMenuItem
            {
                Key = $"source:{normalizedSource.Trim()}",
                Label = normalizedSource.Trim(),
                ContestTrack = "all",
                Source = normalizedSource.Trim()
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

    private static string NormalizeContestSourceForFilters(string? source)
    {
        var normalizedSource = CollapseWhitespace(source);
        if (string.IsNullOrWhiteSpace(normalizedSource))
        {
            return string.Empty;
        }

        return Regex.IsMatch(normalizedSource, @"^General\s+\d{4}$", RegexOptions.IgnoreCase)
            ? "General"
            : normalizedSource;
    }

    private static bool IsBuiltInProblemMenuSource(string source)
    {
        return string.Equals(source, "General", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "OBI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "ICPC Bolivia", StringComparison.OrdinalIgnoreCase);
    }
}
