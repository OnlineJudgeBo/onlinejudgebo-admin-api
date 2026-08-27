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
    public async Task<LearningPathProgressResponse> GetLearningPathProgressAsync(int siteId, string learningPathKey, string userId)
    {
        var progressContext = await BuildLearningPathProgressContextAsync(siteId, learningPathKey);

        var progress = await _academicContext.LearningPathProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.LearningPathId == progressContext.LearningPathId
                && item.UserId == userId);

        var completedTopicIds = await _academicContext.LearningPathTopicProgresses
            .AsNoTracking()
            .Where(item => item.LearningPathId == progressContext.LearningPathId
                && item.UserId == userId)
            .Select(item => item.TopicId)
            .ToListAsync();

        return BuildLearningPathProgressResponse(
            progressContext,
            userId,
            progress?.LastTopicId,
            completedTopicIds,
            progress?.UpdatedAt);
    }

    public async Task<LearningPathProgressResponse> SaveLearningPathProgressAsync(
        int siteId,
        string learningPathKey,
        string userId,
        LearningPathProgressUpdateRequest request)
    {
        var progressContext = await BuildLearningPathProgressContextAsync(siteId, learningPathKey);
        var normalizedCompletedTopicIds = NormalizeLearningPathTopicIds(request.CompletedTopicIds, progressContext.OrderedTopicIds);
        var normalizedCompletedTopicIdSet = normalizedCompletedTopicIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedLastTopicId = ResolveCanonicalLearningPathTopicId(request.LastTopicId, progressContext.OrderedTopicIds);
        var now = DateTime.Now;

        var progress = await _academicContext.LearningPathProgresses
            .FirstOrDefaultAsync(item => item.LearningPathId == progressContext.LearningPathId
                && item.UserId == userId);

        if (progress == null)
        {
            progress = new DbLearningPathProgress
            {
                LearningPathId = progressContext.LearningPathId,
                UserId = userId
            };

            await _academicContext.LearningPathProgresses.AddAsync(progress);
        }

        progress.LastTopicId = normalizedLastTopicId;
        progress.UpdatedAt = now;

        var existingTopicProgress = await _academicContext.LearningPathTopicProgresses
            .Where(item => item.LearningPathId == progressContext.LearningPathId
                && item.UserId == userId)
            .ToListAsync();

        var existingTopicIdSet = existingTopicProgress
            .Select(item => item.TopicId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var progressRowsToRemove = existingTopicProgress
            .Where(item => !normalizedCompletedTopicIdSet.Contains(item.TopicId))
            .ToList();

        if (progressRowsToRemove.Count > 0)
        {
            _academicContext.LearningPathTopicProgresses.RemoveRange(progressRowsToRemove);
        }

        foreach (var topicId in normalizedCompletedTopicIds.Where(topicId => !existingTopicIdSet.Contains(topicId)))
        {
            await _academicContext.LearningPathTopicProgresses.AddAsync(new DbLearningPathTopicProgress
            {
                LearningPathId = progressContext.LearningPathId,
                UserId = userId,
                TopicId = topicId,
                CompletedAt = now
            });
        }

        await _academicContext.SaveChangesAsync();

        return BuildLearningPathProgressResponse(
            progressContext,
            userId,
            normalizedLastTopicId,
            normalizedCompletedTopicIds,
            now);
    }

    public async Task<AcademicSubmissionResponse> SubmitAsync(int siteId, string userId, AcademicSubmissionRequest request)
    {
        var problemExistsInSite = await _context.ProblemSites
            .AnyAsync(problemSite => problemSite.SiteId == siteId && problemSite.IsActive
                && problemSite.problemId == request.ProblemId);

        if (!problemExistsInSite)
        {
            throw new ArgumentException("Problem does not exist for this site.");
        }

        long? courseId = request.CourseId;
        long? assignmentId = request.AssignmentId;
        var now = DateTime.Now;

        if (assignmentId.HasValue)
        {
            var assignment = await _academicContext.CourseAssignments
                .FirstOrDefaultAsync(item => item.AssignmentId == assignmentId.Value);

            if (assignment == null)
            {
                throw new ArgumentException("Assignment not found.");
            }

            if (courseId.HasValue && courseId.Value != assignment.CourseId)
            {
                throw new ArgumentException("Assignment does not belong to the specified course.");
            }

            var assignmentProblemExists = await _academicContext.CourseAssignmentProblems
                .AnyAsync(problem => problem.AssignmentId == assignment.AssignmentId
                    && problem.ProblemId == request.ProblemId
                    && problem.IsVisible);

            if (!assignmentProblemExists)
            {
                throw new ArgumentException("Problem does not belong to the specified assignment.");
            }

            EnsureAssignmentAcceptsSubmissions(assignment, now);

            courseId = assignment.CourseId;
        }

        if (courseId.HasValue)
        {
            var membership = await _academicContext.CourseUsers
                .FirstOrDefaultAsync(member => member.CourseId == courseId.Value
                    && member.UserId == userId);

            if (membership == null)
            {
                throw new UnauthorizedAccessException("User is not a member of the specified course.");
            }
        }

        var solution = new DbSolution
        {
            ProblemId = request.ProblemId,
            UserId = userId,
            Time = 0,
            Memory = 0,
            InDate = now,
            Result = 0,
            Language = (uint)Math.Max(request.LanguageId, 0),
            Ip = request.ClientIp,
            ContestId = request.ContestId,
            Num = 0,
            CodeLength = request.SourceCode.Length,
            PassRate = 0,
            IsRemoteOj = false,
            RemoteId = 0,
            SiteId = siteId
        };

        await _context.Solutions.AddAsync(solution);
        await _context.SaveChangesAsync();

        await _context.SourceCodes.AddAsync(new DbSourceCode
        {
            SolutionId = solution.SolutionId,
            Source = request.SourceCode
        });

        await _context.SaveChangesAsync();

        if (courseId.HasValue && assignmentId.HasValue)
        {
            await _academicContext.CourseSubmissionContexts.AddAsync(new DbCourseSubmissionContext
            {
                SolutionId = solution.SolutionId,
                CourseId = courseId.Value,
                AssignmentId = assignmentId.Value,
                UserId = userId,
                CreatedAt = now
            });

            await _academicContext.SaveChangesAsync();
        }

        return new AcademicSubmissionResponse
        {
            SolutionId = solution.SolutionId,
            LanguageId = request.LanguageId,
            AutoDetected = false,
            CreatedAtUtc = solution.InDate
        };
    }

    private async Task<LearningPathProgressContext> BuildLearningPathProgressContextAsync(int siteId, string learningPathKey)
    {
        var learningPath = await _academicContext.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(path => path.SiteId == siteId && path.LearningPathKey == learningPathKey);

        if (learningPath == null)
        {
            throw new ArgumentException("Learning path not found.");
        }

        var linkedTopics = await _academicContext.LearningPathTopics
            .AsNoTracking()
            .Where(link => link.LearningPathId == learningPath.LearningPathId)
            .ToListAsync();

        if (linkedTopics.Count == 0)
        {
            throw new ArgumentException("Learning path has no stages configured.");
        }

        var topicIds = linkedTopics
            .Select(item => item.TopicId)
            .Distinct()
            .ToList();

        var topics = await _academicContext.Topics
            .AsNoTracking()
            .Where(topic => topicIds.Contains(topic.TopicId))
            .Select(topic => new
            {
                topic.TopicId,
                topic.Name,
                topic.SortOrder
            })
            .ToListAsync();

        var subtopics = await _academicContext.Subtopics
            .AsNoTracking()
            .Where(subtopic => topicIds.Contains(subtopic.TopicId))
            .OrderBy(subtopic => subtopic.SortOrder)
            .ToListAsync();

        var orderedTopics = topics
            .Select((topic, index) => new
            {
                topic.TopicId,
                topic.Name,
                Order = topic.SortOrder > 0 ? topic.SortOrder : ExtractStageOrder(topic.Name, index + 1)
            })
            .OrderBy(item => item.Order)
            .ThenBy(item => item.TopicId)
            .ToList();

        var orderedTopicIds = orderedTopics
            .SelectMany(topic => subtopics
                .Where(subtopic => subtopic.TopicId == topic.TopicId)
                .OrderBy(subtopic => subtopic.SortOrder)
                .Select(BuildLearningPathTopicId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LearningPathProgressContext
        {
            LearningPathId = learningPath.LearningPathId,
            LearningPathKey = learningPath.LearningPathKey,
            OrderedTopicIds = orderedTopicIds,
            ValidTopicIdSet = orderedTopicIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string? ResolveCanonicalLearningPathTopicId(string? topicId, IReadOnlyList<string> orderedTopicIds)
    {
        if (string.IsNullOrWhiteSpace(topicId))
        {
            return null;
        }

        var normalizedTopicId = topicId.Trim();

        return orderedTopicIds.FirstOrDefault(validTopicId =>
            string.Equals(validTopicId, normalizedTopicId, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> NormalizeLearningPathTopicIds(IEnumerable<string>? topicIds, IReadOnlyList<string> orderedTopicIds)
    {
        var requestedTopicIds = (topicIds ?? Array.Empty<string>())
            .Where(topicId => !string.IsNullOrWhiteSpace(topicId))
            .Select(topicId => topicId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return orderedTopicIds
            .Where(requestedTopicIds.Contains)
            .ToList();
    }

    private static LearningPathProgressResponse BuildLearningPathProgressResponse(
        LearningPathProgressContext progressContext,
        string userId,
        string? lastTopicId,
        IEnumerable<string> completedTopicIds,
        DateTime? updatedAtUtc)
    {
        var normalizedCompletedTopicIds = NormalizeLearningPathTopicIds(completedTopicIds, progressContext.OrderedTopicIds);
        var normalizedLastTopicId = ResolveCanonicalLearningPathTopicId(lastTopicId, progressContext.OrderedTopicIds);

        return new LearningPathProgressResponse
        {
            LearningPathKey = progressContext.LearningPathKey,
            UserId = userId,
            LastTopicId = normalizedLastTopicId,
            CompletedTopicIds = normalizedCompletedTopicIds,
            UpdatedAtUtc = updatedAtUtc
        };
    }
}
