using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class AcademicRepository
{
    public async Task<IEnumerable<LearningPathTrackSummary>> GetLearningPathsAsync(int siteId)
    {
        var learningPaths = await _academicContext.LearningPaths
            .Where(path => path.SiteId == siteId)
            .OrderBy(path => path.Title)
            .ThenBy(path => path.LearningPathId)
            .ToListAsync();

        if (learningPaths.Count == 0)
        {
            return Array.Empty<LearningPathTrackSummary>();
        }

        var learningPathIds = learningPaths.Select(path => path.LearningPathId).ToList();
        var learningPathTopics = await _academicContext.LearningPathTopics
            .Where(link => learningPathIds.Contains(link.LearningPathId))
            .ToListAsync();

        var topicIds = learningPathTopics
            .Select(link => link.TopicId)
            .Distinct()
            .ToList();

        var subtopics = topicIds.Count == 0
            ? new List<DbSubtopic>()
            : await _academicContext.Subtopics
                .Where(subtopic => topicIds.Contains(subtopic.TopicId))
                .ToListAsync();

        var subtopicIds = subtopics.Select(subtopic => subtopic.SubtopicId).ToList();
        var subtopicProblems = subtopicIds.Count == 0
            ? new List<DbSubtopicProblem>()
            : await _academicContext.SubtopicProblems
                .Where(link => subtopicIds.Contains(link.SubtopicId))
                .ToListAsync();

        var availableProblemIds = await _context.ProblemSites
            .Where(problemSite => problemSite.SiteId == siteId && problemSite.IsActive)
            .Select(problemSite => problemSite.problemId)
            .ToListAsync();

        var availableProblemIdSet = availableProblemIds.ToHashSet();

        return learningPaths
            .Select(path =>
            {
                var pathTopicIds = learningPathTopics
                    .Where(link => link.LearningPathId == path.LearningPathId)
                    .Select(link => link.TopicId)
                    .Distinct()
                    .ToList();

                var pathSubtopicIds = subtopics
                    .Where(subtopic => pathTopicIds.Contains(subtopic.TopicId))
                    .Select(subtopic => subtopic.SubtopicId)
                    .ToHashSet();

                var estimatedTotalProblems = subtopicProblems
                    .Where(link => pathSubtopicIds.Contains(link.SubtopicId) && availableProblemIdSet.Contains(link.ProblemId))
                    .Select(link => link.ProblemId)
                    .Distinct()
                    .Count();

                return new LearningPathTrackSummary
                {
                    Id = path.LearningPathKey,
                    Title = path.Title,
                    Description = path.Description,
                    Version = path.Version,
                    LanguagePrimary = path.LanguagePrimary,
                    Category = path.Category,
                    TargetAudience = BuildTargetAudience(path),
                    StageCount = pathTopicIds.Count,
                    EstimatedTotalProblems = estimatedTotalProblems
                };
            })
            .OrderBy(path => path.Title)
            .ThenBy(path => path.Id)
            .ToList();
    }

    public async Task<LearningPathResponse> GetLearningPathAsync(int siteId, string learningPathKey)
    {
        var learningPath = await _academicContext.LearningPaths
            .FirstOrDefaultAsync(path => path.SiteId == siteId && path.LearningPathKey == learningPathKey);

        if (learningPath == null)
        {
            throw new ArgumentException("Ruta de aprendizaje no encontrada.");
        }

        var linkedTopics = await _academicContext.LearningPathTopics
            .Where(link => link.LearningPathId == learningPath.LearningPathId)
            .ToListAsync();

        var topicIds = linkedTopics.Select(item => item.TopicId).Distinct().ToList();

        var topics = await _academicContext.Topics
            .Where(topic => topicIds.Contains(topic.TopicId))
            .Select(topic => new { topic.TopicId, topic.TopicKey, topic.Name, topic.SortOrder, topic.UnlockedByDefault })
            .ToListAsync();

        var subtopics = await _academicContext.Subtopics
            .Where(subtopic => topicIds.Contains(subtopic.TopicId))
            .OrderBy(subtopic => subtopic.SortOrder)
            .ToListAsync();

        var subtopicIds = subtopics.Select(subtopic => subtopic.SubtopicId).ToList();

        var availableProblemIds = await _context.ProblemSites
            .Where(problemSite => problemSite.SiteId == siteId && problemSite.IsActive)
            .Select(problemSite => problemSite.problemId)
            .ToListAsync();

        var availableProblemIdSet = availableProblemIds.ToHashSet();

        var subtopicProblemMap = await _academicContext.SubtopicProblems
            .Where(link => subtopicIds.Contains(link.SubtopicId))
            .OrderBy(link => link.SortOrder)
            .ToListAsync();

        var stages = new List<LearningPathStage>();
        var orderedTopics = topics
            .Select((topic, index) => new
            {
                topic.TopicId,
                topic.TopicKey,
                topic.Name,
                topic.SortOrder,
                topic.UnlockedByDefault,
                Order = topic.SortOrder > 0 ? topic.SortOrder : ExtractStageOrder(topic.Name, index + 1)
            })
            .OrderBy(item => item.Order)
            .ThenBy(item => item.TopicId)
            .ToList();

        foreach (var orderedTopic in orderedTopics)
        {
            var stageSubtopics = subtopics
                .Where(subtopic => subtopic.TopicId == orderedTopic.TopicId)
                .OrderBy(subtopic => subtopic.SortOrder)
                .ToList();

            var stageTopics = stageSubtopics
                .Select(subtopic => new LearningPathTopic
                {
                    TopicId = subtopic.SubtopicId,
                    TopicKey = BuildLearningPathTopicId(subtopic),
                    Title = subtopic.Title,
                    Description = subtopic.Summary,
                    Theory = subtopic.Theory,
                    Skills = SplitPipeList(subtopic.LearningObjectives),
                    RecommendedProblems = subtopicProblemMap
                        .Where(link => link.SubtopicId == subtopic.SubtopicId && availableProblemIdSet.Contains(link.ProblemId))
                        .OrderBy(link => link.SortOrder)
                        .Select(link => link.ProblemId)
                        .Distinct()
                        .ToList()
                })
                .ToList();

            var stage = new LearningPathStage
            {
                StageId = orderedTopic.TopicId,
                StageKey = orderedTopic.TopicKey,
                Name = orderedTopic.Name,
                SortOrder = orderedTopic.Order,
                Difficulty = MapDifficulty(stageSubtopics.Select(subtopic => subtopic.DifficultyBand).FirstOrDefault()),
                Description = stageSubtopics.Select(subtopic => subtopic.Summary).FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary)) ?? orderedTopic.Name,
                LearningObjectives = stageSubtopics
                    .SelectMany(subtopic => SplitPipeList(subtopic.LearningObjectives))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList(),
                UnlockedByDefault = orderedTopic.UnlockedByDefault || orderedTopic.Order == orderedTopics.Min(topic => topic.Order),
                Dependencies = new List<long>(),
                Topics = stageTopics
            };

            stages.Add(stage);
        }

        var sortedStages = stages
            .OrderBy(stage => stage.SortOrder)
            .ThenBy(stage => stage.StageId)
            .ToList();

        for (var index = 0; index < sortedStages.Count; index++)
        {
            if (index > 0)
            {
                sortedStages[index].Dependencies.Add(sortedStages[index - 1].StageId);
            }
        }

        var estimatedTotalProblems = sortedStages
            .SelectMany(stage => stage.Topics)
            .SelectMany(topic => topic.RecommendedProblems)
            .Distinct()
            .Count();

        return new LearningPathResponse
        {
            Track = new LearningPathTrack
            {
                Id = learningPath.LearningPathKey,
                Title = learningPath.Title,
                Description = learningPath.Description,
                Version = learningPath.Version,
                LanguagePrimary = learningPath.LanguagePrimary,
                Category = learningPath.Category,
                Slug = learningPath.Slug ?? string.Empty,
                TargetAudience = BuildTargetAudience(learningPath),
                EstimatedTotalProblems = estimatedTotalProblems
            },
            Stages = sortedStages,
            ProgressRules = new LearningPathProgressRules
            {
                UnlockStrategy = "course_controlled",
                AllowFreeExploration = true,
                CourseCanOverrideDependencies = true,
                RecommendNextStageWhenCompleted = true
            }
        };
    }

    public async Task<LearningPathResponse> CreateLearningPathAsync(int siteId, LearningPathAdminUpsertRequest request)
    {
        var key = NormalizeCatalogKey(request.Key);
        if (await _academicContext.LearningPaths.AnyAsync(path => path.SiteId == siteId && path.LearningPathKey == key))
            throw new ArgumentException("La clave de la ruta de aprendizaje ya existe.");

        await _academicContext.LearningPaths.AddAsync(new DbLearningPath
        {
            SiteId = siteId,
            LearningPathKey = key,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Version = request.Version,
            LanguagePrimary = request.LanguagePrimary?.Trim() ?? string.Empty,
            Category = request.Category?.Trim() ?? string.Empty,
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? key : request.Slug.Trim()
        });
        await _academicContext.SaveChangesAsync();
        return await GetLearningPathAsync(siteId, key);
    }

    public async Task<LearningPathResponse> UpdateLearningPathAsync(int siteId, string learningPathKey, LearningPathAdminUpsertRequest request)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        var key = NormalizeCatalogKey(request.Key);
        if (!string.Equals(path.LearningPathKey, key, StringComparison.Ordinal))
            throw new ArgumentException("La clave de la ruta de aprendizaje no se puede cambiar.");
        path.Title = request.Title.Trim();
        path.Description = request.Description?.Trim() ?? string.Empty;
        path.Version = request.Version;
        path.LanguagePrimary = request.LanguagePrimary?.Trim() ?? string.Empty;
        path.Category = request.Category?.Trim() ?? string.Empty;
        path.Slug = string.IsNullOrWhiteSpace(request.Slug) ? key : request.Slug.Trim();
        await _academicContext.SaveChangesAsync();
        return await GetLearningPathAsync(siteId, path.LearningPathKey);
    }

    public async Task DeleteLearningPathAsync(int siteId, string learningPathKey)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);

        var stageIds = await _academicContext.LearningPathTopics
            .Where(link => link.LearningPathId == path.LearningPathId)
            .Select(link => link.TopicId)
            .ToListAsync();

        // Stages also linked to another learning path must only be unlinked
        // here, not deleted outright. Resolved in one query for every stage
        // instead of DeleteStageDataAsync's per-stage AnyAsync check - this
        // whole method used to do O(stages) round trips via that helper,
        // now it's a small constant number regardless of stage count.
        var sharedStageIds = (await _academicContext.LearningPathTopics
            .Where(link => stageIds.Contains(link.TopicId) && link.LearningPathId != path.LearningPathId)
            .Select(link => link.TopicId)
            .Distinct()
            .ToListAsync()).ToHashSet();
        var exclusiveStageIds = stageIds.Where(id => !sharedStageIds.Contains(id)).ToList();

        if (exclusiveStageIds.Count > 0)
        {
            var subtopicIds = await _academicContext.Subtopics
                .Where(item => exclusiveStageIds.Contains(item.TopicId))
                .Select(item => item.SubtopicId)
                .ToListAsync();
            _academicContext.SubtopicProblems.RemoveRange(_academicContext.SubtopicProblems.Where(item => subtopicIds.Contains(item.SubtopicId)));
            _academicContext.SubtopicTags.RemoveRange(_academicContext.SubtopicTags.Where(item => subtopicIds.Contains(item.SubtopicId)));
            _academicContext.Subtopics.RemoveRange(_academicContext.Subtopics.Where(item => exclusiveStageIds.Contains(item.TopicId)));
            _academicContext.Topics.RemoveRange(_academicContext.Topics.Where(item => exclusiveStageIds.Contains(item.TopicId)));
        }

        _academicContext.LearningPathTopics.RemoveRange(_academicContext.LearningPathTopics.Where(item => item.LearningPathId == path.LearningPathId));
        _academicContext.LearningPathProgresses.RemoveRange(_academicContext.LearningPathProgresses.Where(item => item.LearningPathId == path.LearningPathId));
        _academicContext.LearningPathTopicProgresses.RemoveRange(_academicContext.LearningPathTopicProgresses.Where(item => item.LearningPathId == path.LearningPathId));
        await _academicContext.SaveChangesAsync();

        _academicContext.LearningPaths.Remove(path);
        await _academicContext.SaveChangesAsync();
    }

    public async Task<LearningPathResponse> CreateLearningPathStageAsync(int siteId, string learningPathKey, LearningPathStageAdminRequest request)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        var key = NormalizeCatalogKey(request.Key);
        if (await _academicContext.Topics.AnyAsync(topic => topic.TopicKey == key)) throw new ArgumentException("La clave de la etapa ya existe.");
        if (await HasStageWithOrderAsync(path.LearningPathId, request.Order)) throw new ArgumentException("El orden de la etapa ya existe en esta ruta de aprendizaje.");
        await using var transaction = await _academicContext.Database.BeginTransactionAsync();
        var stage = new DbAcademicTopic { TopicKey = key, Name = request.Name.Trim(), SortOrder = request.Order, UnlockedByDefault = request.UnlockedByDefault };
        await _academicContext.Topics.AddAsync(stage);
        await _academicContext.SaveChangesAsync();
        await _academicContext.LearningPathTopics.AddAsync(new DbLearningPathTopic { LearningPathId = path.LearningPathId, TopicId = stage.TopicId });
        await _academicContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetLearningPathAsync(siteId, path.LearningPathKey);
    }

    public async Task<LearningPathResponse> LinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        var stage = await _academicContext.Topics.FirstOrDefaultAsync(item => item.TopicId == stageId)
            ?? throw new ArgumentException("Etapa no encontrada.");
        if (await _academicContext.LearningPathTopics.AnyAsync(link => link.LearningPathId == path.LearningPathId && link.TopicId == stageId))
            throw new ArgumentException("La etapa ya está vinculada a esta ruta de aprendizaje.");
        if (await HasStageWithOrderAsync(path.LearningPathId, stage.SortOrder))
            throw new ArgumentException("El orden de la etapa ya existe en esta ruta de aprendizaje.");

        await _academicContext.LearningPathTopics.AddAsync(new DbLearningPathTopic
        {
            LearningPathId = path.LearningPathId,
            TopicId = stageId
        });
        await _academicContext.SaveChangesAsync();
        return await GetLearningPathAsync(siteId, path.LearningPathKey);
    }

    public async Task UnlinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        await EnsureStageLinkedAsync(path.LearningPathId, stageId);
        await RemoveStageLinkAndProgressAsync(path.LearningPathId, stageId);
        await _academicContext.SaveChangesAsync();
    }

    public async Task<LearningPathResponse> UpdateLearningPathStageAsync(int siteId, string learningPathKey, long stageId, LearningPathStageAdminRequest request)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        await EnsureStageLinkedAsync(path.LearningPathId, stageId);
        await EnsureStageIsExclusiveAsync(path.LearningPathId, stageId);
        var stage = await _academicContext.Topics.FirstAsync(item => item.TopicId == stageId);
        var key = NormalizeCatalogKey(request.Key);
        if (await _academicContext.Topics.AnyAsync(item => item.TopicId != stageId && item.TopicKey == key)) throw new ArgumentException("La clave de la etapa ya existe.");
        if (await HasStageWithOrderAsync(path.LearningPathId, request.Order, stageId)) throw new ArgumentException("El orden de la etapa ya existe en esta ruta de aprendizaje.");
        stage.TopicKey = key; stage.Name = request.Name.Trim(); stage.SortOrder = request.Order; stage.UnlockedByDefault = request.UnlockedByDefault;
        await _academicContext.SaveChangesAsync();
        return await GetLearningPathAsync(siteId, path.LearningPathKey);
    }

    public async Task DeleteLearningPathStageAsync(int siteId, string learningPathKey, long stageId)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        await EnsureStageLinkedAsync(path.LearningPathId, stageId);
        await EnsureStageIsExclusiveAsync(path.LearningPathId, stageId);
        await DeleteStageDataAsync(path.LearningPathId, stageId);
        await _academicContext.SaveChangesAsync();
    }

    public async Task<LearningPathResponse> CreateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, LearningPathTopicAdminRequest request)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        await EnsureStageLinkedAsync(path.LearningPathId, stageId);
        await EnsureStageIsExclusiveAsync(path.LearningPathId, stageId);
        var key = NormalizeCatalogKey(request.Key);
        if (await _academicContext.Subtopics.AnyAsync(item => item.TopicId == stageId && item.SubtopicKey == key)) throw new ArgumentException("La clave del tema ya existe en esta etapa.");
        var validProblemIds = await ValidateProblemIdsAsync(siteId, request.ProblemIds);
        await using var transaction = await _academicContext.Database.BeginTransactionAsync();
        var topic = new DbSubtopic { TopicId = stageId, SubtopicKey = key };
        ApplyTopic(topic, request);
        await _academicContext.Subtopics.AddAsync(topic);
        await _academicContext.SaveChangesAsync();
        await ReplaceTopicProblemsAsync(topic.SubtopicId, validProblemIds);
        await transaction.CommitAsync();
        return await GetLearningPathAsync(siteId, path.LearningPathKey);
    }

    public async Task<LearningPathResponse> UpdateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, LearningPathTopicAdminRequest request)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        await EnsureStageLinkedAsync(path.LearningPathId, stageId);
        await EnsureStageIsExclusiveAsync(path.LearningPathId, stageId);
        var topic = await _academicContext.Subtopics.FirstOrDefaultAsync(item => item.SubtopicId == topicId && item.TopicId == stageId) ?? throw new ArgumentException("Tema no encontrado.");
        var key = NormalizeCatalogKey(request.Key);
        if (await _academicContext.Subtopics.AnyAsync(item => item.SubtopicId != topicId && item.TopicId == stageId && item.SubtopicKey == key)) throw new ArgumentException("La clave del tema ya existe en esta etapa.");
        var previousKey = topic.SubtopicKey;
        topic.SubtopicKey = key; ApplyTopic(topic, request);
        if (!string.Equals(previousKey, key, StringComparison.OrdinalIgnoreCase))
        {
            // TopicId is part of LearningPathTopicProgress's primary key, so it
            // can't be updated in place (EF rejects that outright) - delete the
            // old-keyed rows and re-insert them under the new key instead.
            var topicProgress = await _academicContext.LearningPathTopicProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.TopicId == previousKey).ToListAsync();
            _academicContext.LearningPathTopicProgresses.RemoveRange(topicProgress);
            await _academicContext.LearningPathTopicProgresses.AddRangeAsync(topicProgress.Select(item => new DbLearningPathTopicProgress
            {
                LearningPathId = item.LearningPathId,
                UserId = item.UserId,
                TopicId = key,
                CompletedAt = item.CompletedAt
            }));
            var pathProgress = await _academicContext.LearningPathProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.LastTopicId == previousKey).ToListAsync();
            pathProgress.ForEach(item => item.LastTopicId = key);
        }
        var validProblemIds = await ValidateProblemIdsAsync(siteId, request.ProblemIds);
        await ReplaceTopicProblemsAsync(topic.SubtopicId, validProblemIds);
        return await GetLearningPathAsync(siteId, path.LearningPathKey);
    }

    public async Task DeleteLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId)
    {
        var path = await FindLearningPathAsync(siteId, learningPathKey);
        await EnsureStageLinkedAsync(path.LearningPathId, stageId);
        await EnsureStageIsExclusiveAsync(path.LearningPathId, stageId);
        var topic = await _academicContext.Subtopics.FirstOrDefaultAsync(item => item.SubtopicId == topicId && item.TopicId == stageId) ?? throw new ArgumentException("Tema no encontrado.");
        _academicContext.SubtopicProblems.RemoveRange(_academicContext.SubtopicProblems.Where(item => item.SubtopicId == topicId));
        _academicContext.SubtopicTags.RemoveRange(_academicContext.SubtopicTags.Where(item => item.SubtopicId == topicId));
        _academicContext.LearningPathTopicProgresses.RemoveRange(_academicContext.LearningPathTopicProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.TopicId == topic.SubtopicKey));
        var pathProgress = await _academicContext.LearningPathProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.LastTopicId == topic.SubtopicKey).ToListAsync();
        pathProgress.ForEach(item => item.LastTopicId = null);
        _academicContext.Subtopics.Remove(topic);
        await _academicContext.SaveChangesAsync();
    }

    private async Task<DbLearningPath> FindLearningPathAsync(int siteId, string key) =>
        await _academicContext.LearningPaths.FirstOrDefaultAsync(path => path.SiteId == siteId && path.LearningPathKey == key) ?? throw new ArgumentException("Ruta de aprendizaje no encontrada.");

    private async Task EnsureStageLinkedAsync(long pathId, long stageId)
    {
        if (!await _academicContext.LearningPathTopics.AnyAsync(link => link.LearningPathId == pathId && link.TopicId == stageId)) throw new ArgumentException("Etapa no encontrada en la ruta de aprendizaje.");
    }

    private async Task EnsureStageIsExclusiveAsync(long pathId, long stageId)
    {
        if (await _academicContext.LearningPathTopics.AnyAsync(link => link.TopicId == stageId && link.LearningPathId != pathId))
            throw new InvalidOperationException("Las etapas compartidas no se pueden modificar. Primero quita la etapa de las otras rutas de aprendizaje.");
    }

    private Task<bool> HasStageWithOrderAsync(long pathId, int order, long? excludedStageId = null) =>
        (from link in _academicContext.LearningPathTopics
         join stage in _academicContext.Topics on link.TopicId equals stage.TopicId
         where link.LearningPathId == pathId
            && stage.SortOrder == order
            && (!excludedStageId.HasValue || stage.TopicId != excludedStageId.Value)
         select stage.TopicId).AnyAsync();

    private async Task DeleteStageDataAsync(long pathId, long stageId)
    {
        var isShared = await _academicContext.LearningPathTopics
            .AnyAsync(link => link.TopicId == stageId && link.LearningPathId != pathId);
        await RemoveStageLinkAndProgressAsync(pathId, stageId);
        await _academicContext.SaveChangesAsync();
        if (isShared) return;

        var topicIds = await _academicContext.Subtopics.Where(item => item.TopicId == stageId).Select(item => item.SubtopicId).ToListAsync();
        _academicContext.SubtopicProblems.RemoveRange(_academicContext.SubtopicProblems.Where(item => topicIds.Contains(item.SubtopicId)));
        _academicContext.SubtopicTags.RemoveRange(_academicContext.SubtopicTags.Where(item => topicIds.Contains(item.SubtopicId)));
        await _academicContext.SaveChangesAsync();

        _academicContext.Subtopics.RemoveRange(_academicContext.Subtopics.Where(item => item.TopicId == stageId));
        await _academicContext.SaveChangesAsync();

        var stage = await _academicContext.Topics.FirstOrDefaultAsync(item => item.TopicId == stageId);
        if (stage != null)
        {
            _academicContext.Topics.Remove(stage);
            await _academicContext.SaveChangesAsync();
        }
    }

    private async Task RemoveStageLinkAndProgressAsync(long pathId, long stageId)
    {
        var topicKeys = await _academicContext.Subtopics
            .Where(item => item.TopicId == stageId)
            .Select(item => item.SubtopicKey)
            .ToListAsync();
        _academicContext.LearningPathTopics.RemoveRange(_academicContext.LearningPathTopics
            .Where(item => item.LearningPathId == pathId && item.TopicId == stageId));
        _academicContext.LearningPathTopicProgresses.RemoveRange(_academicContext.LearningPathTopicProgresses
            .Where(item => item.LearningPathId == pathId && topicKeys.Contains(item.TopicId)));
        var pathProgress = await _academicContext.LearningPathProgresses
            .Where(item => item.LearningPathId == pathId && item.LastTopicId != null && topicKeys.Contains(item.LastTopicId))
            .ToListAsync();
        pathProgress.ForEach(item => item.LastTopicId = null);
    }

    private async Task<List<int>> ValidateProblemIdsAsync(int siteId, IEnumerable<int> problemIds)
    {
        var ids = problemIds.Where(id => id > 0).Distinct().ToList();
        var validIds = await _context.ProblemSites.Where(item => item.SiteId == siteId && item.IsActive && ids.Contains(item.problemId)).Select(item => item.problemId).ToListAsync();
        if (validIds.Count != ids.Count) throw new ArgumentException("Uno o más problemas no están activos en este sitio.");
        return ids;
    }

    private async Task ReplaceTopicProblemsAsync(long topicId, IReadOnlyList<int> problemIds)
    {
        _academicContext.SubtopicProblems.RemoveRange(_academicContext.SubtopicProblems.Where(item => item.SubtopicId == topicId));
        await _academicContext.SubtopicProblems.AddRangeAsync(problemIds.Select((id, index) => new DbSubtopicProblem { SubtopicId = topicId, ProblemId = id, RoleInTopic = "core", SortOrder = index + 1 }));
        await _academicContext.SaveChangesAsync();
    }

    private static void ApplyTopic(DbSubtopic topic, LearningPathTopicAdminRequest request)
    {
        topic.Title = request.Title.Trim(); topic.Summary = request.Summary?.Trim(); topic.Theory = request.Theory;
        topic.LearningObjectives = string.Join('|', request.LearningObjectives.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        topic.DifficultyBand = request.Difficulty?.Trim(); topic.SortOrder = request.Order;
    }

    private static string NormalizeCatalogKey(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9_]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("La clave no contiene caracteres válidos.");
        return normalized;
    }

    private static List<string> SplitPipeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildLearningPathTopicId(DbSubtopic subtopic)
    {
        return string.IsNullOrWhiteSpace(subtopic.SubtopicKey)
            ? $"subtopic_{subtopic.SubtopicId}"
            : subtopic.SubtopicKey.Trim();
    }

    private static List<string> BuildTargetAudience(DbLearningPath learningPath)
    {
        var audience = new List<string>();
        var language = learningPath.LanguagePrimary?.Trim();
        var category = learningPath.Category?.Trim();

        if (!string.IsNullOrWhiteSpace(language))
        {
            audience.Add($"Estudiantes que practican {FormatAudienceValue(language)}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            audience.Add(DescribeAudienceCategory(category));
        }

        audience.Add("Participantes de concursos de programación");

        return audience
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatAudienceValue(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "cpp" => "C++",
            "c++" => "C++",
            "py" => "Python",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.Replace('_', ' ').Replace('-', ' '))
        };
    }

    private static string DescribeAudienceCategory(string category)
    {
        var normalized = category.Trim().ToLowerInvariant();

        if (normalized.Contains("univers"))
        {
            return "Estudiantes universitarios";
        }

        if (normalized.Contains("coleg") || normalized.Contains("school"))
        {
            return "Estudiantes de colegio";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.Replace('_', ' ').Replace('-', ' '));
    }

    private static int ExtractStageOrder(string name, int fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var match = Regex.Match(name, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var parsed))
        {
            return parsed == 0 ? 1 : parsed;
        }

        return fallback;
    }

    private static string MapDifficulty(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "basico";
        }

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Contains("advanced") || normalized.Contains("avanz"))
        {
            return "avanzado";
        }

        if (normalized.Contains("intermediate") || normalized.Contains("intermedio") || normalized.Contains("low_intermediate"))
        {
            return "intermedio";
        }

        return "basico";
    }
}
