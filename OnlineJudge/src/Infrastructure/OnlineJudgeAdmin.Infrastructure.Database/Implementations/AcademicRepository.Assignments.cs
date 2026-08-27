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
    public async Task<AcademicCourseAssignment> CreateCourseAssignmentAsync(
        int siteId,
        long courseId,
        string userId,
        AcademicCourseAssignmentCreationRequest request)
    {
        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Curso no encontrado.");
        }

        await EnsureAssignmentContentItemsAsync(courseId, userId);

        var problemIds = request.ProblemIds
            .Where(problemId => problemId > 0)
            .Distinct()
            .ToList();

        var availableProblemIds = await _context.ProblemSites
            .Where(problemSite => problemSite.SiteId == siteId && problemSite.IsActive
                && problemIds.Contains(problemSite.problemId))
            .Select(problemSite => problemSite.problemId)
            .Distinct()
            .ToListAsync();

        if (availableProblemIds.Count != problemIds.Count)
        {
            throw new ArgumentException("Algunos problemas no están disponibles en este sitio.");
        }

        var assignment = new DbCourseAssignment
        {
            CourseId = courseId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OpensAt = request.OpensAt ?? DateTime.Now,
            DueAt = request.DueAt ?? DateTime.Now.AddDays(7),
            LateDueAt = request.LateDueAt,
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now,
            CreatedByUserId = userId
        };

        await _academicContext.CourseAssignments.AddAsync(assignment);
        await _academicContext.SaveChangesAsync();

        var assignmentProblems = availableProblemIds
            .OrderBy(problemId => problemId)
            .Select(problemId => new DbCourseAssignmentProblem
            {
                AssignmentId = assignment.AssignmentId,
                ProblemId = problemId,
                Points = 100,
                IsVisible = true
            })
            .ToList();

        await _academicContext.CourseAssignmentProblems.AddRangeAsync(assignmentProblems);
        var nextPosition = (await _academicContext.CourseContentItems
            .Where(item => item.CourseId == courseId)
            .MaxAsync(item => (int?)item.Position) ?? 0) + 10;
        await _academicContext.CourseContentItems.AddAsync(new DbCourseContentItem
        {
            CourseId = courseId,
            ItemType = "contest",
            Title = assignment.Title,
            Description = assignment.Description,
            AssignmentId = assignment.AssignmentId,
            Position = nextPosition,
            IsPublished = assignment.IsActive,
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now
        });
        await _academicContext.SaveChangesAsync();

        var assignments = await BuildCourseAssignmentsAsync(siteId, courseId, string.Empty);
        return assignments.First(item => item.AssignmentId == assignment.AssignmentId);
    }

    public async Task<AcademicCourseContentItem> CreateCourseMaterialAsync(
        long courseId,
        string userId,
        AcademicCourseMaterialCreationRequest request)
    {
        await EnsureAssignmentContentItemsAsync(courseId, userId);
        var nextPosition = (await _academicContext.CourseContentItems
            .Where(item => item.CourseId == courseId)
            .MaxAsync(item => (int?)item.Position) ?? 0) + 10;
        var material = new DbCourseContentItem
        {
            CourseId = courseId,
            ItemType = "material",
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ContentUrl = string.IsNullOrWhiteSpace(request.ContentUrl) ? null : request.ContentUrl.Trim(),
            ContentBody = string.IsNullOrWhiteSpace(request.ContentBody) ? null : request.ContentBody.Trim(),
            Position = nextPosition,
            IsPublished = request.IsPublished,
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now
        };
        await _academicContext.CourseContentItems.AddAsync(material);
        await _academicContext.SaveChangesAsync();
        return new AcademicCourseContentItem
        {
            ItemId = material.ItemId, CourseId = courseId, Type = material.ItemType,
            Title = material.Title, Description = material.Description, ContentUrl = material.ContentUrl,
            ContentBody = material.ContentBody, Position = material.Position, IsPublished = material.IsPublished
        };
    }

    public async Task<AcademicCourseContentItem> UpdateCourseMaterialAsync(
        long courseId,
        long materialId,
        AcademicCourseMaterialCreationRequest request)
    {
        var material = await _academicContext.CourseContentItems
            .FirstOrDefaultAsync(item => item.CourseId == courseId && item.ItemId == materialId && item.ItemType == "material")
            ?? throw new KeyNotFoundException("No se encontró el material del curso.");
        material.Title = request.Title.Trim();
        material.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        material.ContentUrl = string.IsNullOrWhiteSpace(request.ContentUrl) ? null : request.ContentUrl.Trim();
        material.ContentBody = string.IsNullOrWhiteSpace(request.ContentBody) ? null : request.ContentBody.Trim();
        material.IsPublished = request.IsPublished;
        await _academicContext.SaveChangesAsync();
        return new AcademicCourseContentItem
        {
            ItemId = material.ItemId, CourseId = courseId, Type = material.ItemType,
            Title = material.Title, Description = material.Description, ContentUrl = material.ContentUrl,
            ContentBody = material.ContentBody, Position = material.Position, IsPublished = material.IsPublished
        };
    }

    public async Task DeleteCourseMaterialAsync(long courseId, long materialId)
    {
        var material = await _academicContext.CourseContentItems
            .FirstOrDefaultAsync(item => item.CourseId == courseId && item.ItemId == materialId && item.ItemType == "material")
            ?? throw new KeyNotFoundException("No se encontró el material del curso.");
        _academicContext.CourseContentItems.Remove(material);
        await _academicContext.SaveChangesAsync();
    }

    private async Task EnsureAssignmentContentItemsAsync(long courseId, string userId)
    {
        var linkedIds = await _academicContext.CourseContentItems
            .Where(item => item.CourseId == courseId && item.AssignmentId.HasValue)
            .Select(item => item.AssignmentId!.Value)
            .ToListAsync();
        var missing = await _academicContext.CourseAssignments
            .Where(item => item.CourseId == courseId && !linkedIds.Contains(item.AssignmentId))
            .OrderBy(item => item.OpensAt)
            .ThenBy(item => item.AssignmentId)
            .ToListAsync();
        if (missing.Count == 0) return;
        var position = (await _academicContext.CourseContentItems
            .Where(item => item.CourseId == courseId)
            .MaxAsync(item => (int?)item.Position) ?? 0) + 10;
        foreach (var assignment in missing)
        {
            await _academicContext.CourseContentItems.AddAsync(new DbCourseContentItem
            {
                CourseId = courseId, ItemType = "contest", Title = assignment.Title,
                Description = assignment.Description, AssignmentId = assignment.AssignmentId,
                Position = position, IsPublished = assignment.IsActive,
                CreatedByUserId = assignment.CreatedByUserId ?? userId, CreatedAt = assignment.CreatedAt
            });
            position += 10;
        }
        await _academicContext.SaveChangesAsync();
    }

    public async Task<AcademicCourseAssignment> UpdateCourseAssignmentAsync(
        int siteId,
        long courseId,
        long assignmentId,
        string userId,
        AcademicCourseAssignmentCreationRequest request)
    {
        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Curso no encontrado.");
        }

        var assignment = await _academicContext.CourseAssignments
            .FirstOrDefaultAsync(item => item.AssignmentId == assignmentId
                && item.CourseId == courseId);

        if (assignment == null)
        {
            throw new ArgumentException("Tarea no encontrada en este curso.");
        }

        var problemIds = request.ProblemIds
            .Where(problemId => problemId > 0)
            .Distinct()
            .ToList();

        var availableProblemIds = await _context.ProblemSites
            .Where(problemSite => problemSite.SiteId == siteId && problemSite.IsActive
                && problemIds.Contains(problemSite.problemId))
            .Select(problemSite => problemSite.problemId)
            .Distinct()
            .ToListAsync();

        if (availableProblemIds.Count != problemIds.Count)
        {
            throw new ArgumentException("Algunos problemas no están disponibles en este sitio.");
        }

        assignment.Title = request.Title.Trim();
        assignment.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        assignment.OpensAt = request.OpensAt ?? assignment.OpensAt;
        assignment.DueAt = request.DueAt ?? assignment.DueAt;
        assignment.LateDueAt = request.LateDueAt;
        assignment.IsActive = request.IsActive;

        var existingAssignmentProblems = await _academicContext.CourseAssignmentProblems
            .Where(problem => problem.AssignmentId == assignmentId)
            .ToListAsync();
        var existingProblemMap = existingAssignmentProblems
            .ToDictionary(problem => problem.ProblemId, problem => problem);
        var desiredProblemIds = availableProblemIds.ToHashSet();

        var problemsToRemove = existingAssignmentProblems
            .Where(problem => !desiredProblemIds.Contains(problem.ProblemId))
            .ToList();
        if (problemsToRemove.Count > 0)
        {
            _academicContext.CourseAssignmentProblems.RemoveRange(problemsToRemove);
        }

        var problemsToAdd = availableProblemIds
            .Where(problemId => !existingProblemMap.ContainsKey(problemId))
            .OrderBy(problemId => problemId)
            .Select(problemId => new DbCourseAssignmentProblem
            {
                AssignmentId = assignmentId,
                ProblemId = problemId,
                Points = 100,
                IsVisible = true
            })
            .ToList();

        if (problemsToAdd.Count > 0)
        {
            await _academicContext.CourseAssignmentProblems.AddRangeAsync(problemsToAdd);
        }

        await _academicContext.SaveChangesAsync();

        var assignments = await BuildCourseAssignmentsAsync(siteId, courseId, string.Empty);
        return assignments.First(item => item.AssignmentId == assignment.AssignmentId);
    }

    public async Task<AcademicCourseAssignmentDetailResponse> GetCourseAssignmentAsync(int siteId, long courseId, long assignmentId, string currentUserId)
    {
        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Curso no encontrado.");
        }

        var assignment = (await BuildCourseAssignmentsAsync(siteId, courseId, currentUserId))
            .FirstOrDefault(item => item.AssignmentId == assignmentId);

        if (assignment == null)
        {
            throw new ArgumentException("Tarea no encontrada.");
        }

        var studentUserIds = await _academicContext.CourseUsers
            .Where(member => member.CourseId == courseId && member.Role == CourseRoleNames.Student)
            .OrderBy(member => member.UserId)
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync();
        var scopedSolutions = studentUserIds.Count == 0
            ? new List<CourseScopedSolutionRow>()
            : await BuildScopedCourseSolutionsAsync(siteId, courseId, new[] { assignmentId }, userIds: studentUserIds);
        var solutionsByUser = scopedSolutions
            .GroupBy(solution => solution.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var nickMap = studentUserIds.Count == 0
            ? new Dictionary<string, string>()
            : await ResolveNickMapAsync(siteId, studentUserIds);
        var rankingItems = studentUserIds
            .Select(userId =>
            {
                var userSolutions = solutionsByUser.TryGetValue(userId, out var rowsByUser)
                    ? rowsByUser
                    : new List<CourseScopedSolutionRow>();
                var submissions = userSolutions.Count;
                var accepted = userSolutions.Count(solution => solution.Result == AcceptedResultCode);
                var solved = userSolutions
                    .Where(solution => solution.Result == AcceptedResultCode)
                    .Select(solution => solution.ProblemId)
                    .Distinct()
                    .Count();

                return new AcademicCourseAssignmentRankingItem
                {
                    UserId = userId,
                    Nick = nickMap.TryGetValue(userId, out var nick) ? nick : userId,
                    Solved = solved,
                    Submissions = submissions,
                    Accepted = accepted,
                    Accuracy = submissions > 0
                        ? Math.Round((decimal)accepted * 100m / submissions, 1)
                        : 0m
                };
            })
            .OrderByDescending(item => item.Solved)
            .ThenByDescending(item => item.Accepted)
            .ThenBy(item => item.Submissions)
            .ThenBy(item => item.UserId)
            .ToList();

        for (var index = 0; index < rankingItems.Count; index++)
        {
            rankingItems[index].Rank = index + 1;
        }

        return new AcademicCourseAssignmentDetailResponse
        {
            CourseId = courseId,
            CourseName = course.Name,
            CourseDescription = course.Description,
            GeneratedAtUtc = DateTime.Now,
            StudentCount = studentUserIds.Count,
            ParticipantCount = rankingItems.Count(item => item.Submissions > 0),
            TotalSubmissions = scopedSolutions.Count,
            TotalAccepted = scopedSolutions.Count(solution => solution.Result == AcceptedResultCode),
            Assignment = assignment,
            Items = rankingItems
        };
    }

    public async Task<PublicSubmissionsResponse> GetCourseAssignmentSubmissionsAsync(
        int siteId,
        long courseId,
        long assignmentId,
        int page,
        int pageSize)
    {
        var assignmentExists = await _academicContext.CourseAssignments
            .AnyAsync(item => item.CourseId == courseId && item.AssignmentId == assignmentId);

        if (!assignmentExists)
        {
            throw new ArgumentException("Tarea no encontrada.");
        }

        var studentUserIds = await _academicContext.CourseUsers
            .Where(member => member.CourseId == courseId && member.Role == CourseRoleNames.Student)
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync();

        if (studentUserIds.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = siteId,
                Total = 0,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.Now,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var scopedSolutionIdsQuery = _academicContext.CourseSubmissionContexts
            .Where(submissionContext => submissionContext.CourseId == courseId
                && submissionContext.AssignmentId == assignmentId
                && studentUserIds.Contains(submissionContext.UserId))
            .Select(submissionContext => submissionContext.SolutionId);

        var total = await scopedSolutionIdsQuery.CountAsync();

        var pagedSolutionIds = await scopedSolutionIdsQuery
            .OrderByDescending(solutionId => solutionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (pagedSolutionIds.Count == 0)
        {
            return new PublicSubmissionsResponse
            {
                SiteId = siteId,
                Total = total,
                Page = page,
                PageSize = pageSize,
                UpdatedAtUtc = DateTime.Now,
                Items = Array.Empty<PublicSubmissionListItem>()
            };
        }

        var pagedSolutionIdInts = pagedSolutionIds
            .Select(solutionId => (int)solutionId)
            .ToList();
        var solutionOrder = pagedSolutionIdInts
            .Select((solutionId, index) => new { solutionId, index })
            .ToDictionary(item => item.solutionId, item => item.index);

        var solutions = await _context.Solutions
            .Where(solution => solution.SiteId == siteId
                && pagedSolutionIdInts.Contains(solution.SolutionId))
            .ToListAsync();
        solutions = solutions
            .OrderBy(solution => solutionOrder.GetValueOrDefault(solution.SolutionId, int.MaxValue))
            .ToList();

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
            UpdatedAtUtc = DateTime.Now,
            Items = items
        };
    }

    public async Task<bool> CanViewCourseSubmissionSourceAsync(int siteId, int solutionId, string userId, bool includeAdminAccess)
    {
        var solutionBelongsToSite = await _context.Solutions
            .AnyAsync(solution => solution.SiteId == siteId && solution.SolutionId == solutionId);

        if (!solutionBelongsToSite)
        {
            return false;
        }

        var submissionContext = await _academicContext.CourseSubmissionContexts
            .Where(item => item.SolutionId == solutionId)
            .Select(item => new
            {
                item.CourseId
            })
            .FirstOrDefaultAsync();

        if (submissionContext == null)
        {
            return false;
        }

        if (includeAdminAccess)
        {
            return true;
        }

        var courseOwnerUserId = await _academicContext.Courses
            .Where(course => course.CourseId == submissionContext.CourseId)
            .Select(course => course.CreatedByUserId)
            .FirstOrDefaultAsync();

        if (string.Equals(courseOwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await _academicContext.CourseUsers
            .AnyAsync(member => member.CourseId == submissionContext.CourseId
                && member.UserId == userId
                && (member.Role == CourseRoleNames.Admin || member.Role == CourseRoleNames.Teacher || member.Role == CourseRoleNames.Assistant));
    }

    public async Task<IEnumerable<AcademicCourseRankingItem>> GetCourseRankingAsync(int siteId, long courseId)
    {
        var report = await BuildCourseReportAsync(siteId, courseId, includeOwner: false);
        var assignmentColumns = report.Assignments.ToDictionary(assignment => assignment.AssignmentId);
        var rows = report.Items
            .Select(item => new AcademicCourseRankingItem
            {
                UserId = item.UserId,
                Nick = item.Nick,
                TotalScore = item.TotalSolved * 100m,
                Solved = item.TotalSolved,
                Attempts = item.TotalAttempts,
                Assignments = item.Assignments
                    .Select(assignmentCell =>
                    {
                        assignmentColumns.TryGetValue(assignmentCell.AssignmentId, out var assignmentColumn);

                        return new AcademicCourseRankingAssignmentSummary
                        {
                            AssignmentId = assignmentCell.AssignmentId,
                            Title = assignmentColumn?.Title ?? $"Contest {assignmentCell.AssignmentId}",
                            ProblemCount = assignmentColumn?.ProblemCount ?? 0,
                            Solved = assignmentCell.Solved
                        };
                    })
                    .ToList()
            })
            .ToList();

        for (var index = 0; index < rows.Count; index++)
        {
            rows[index].Rank = index + 1;
        }

        return rows;
    }

    public Task<AcademicCourseReportResponse> GetCourseReportAsync(int siteId, long courseId)
    {
        return BuildCourseReportAsync(siteId, courseId, includeOwner: true);
    }

    public async Task<AcademicStudentProgress> GetStudentProgressAsync(int siteId, long courseId, string userId)
    {
        var nick = await ResolveNickMapAsync(siteId, new[] { userId });
        var assignments = await _academicContext.CourseAssignments
            .Where(assignment => assignment.CourseId == courseId)
            .OrderBy(assignment => assignment.OpensAt)
            .ThenBy(assignment => assignment.DueAt)
            .ThenBy(assignment => assignment.AssignmentId)
            .ToListAsync();
        var assignmentIdSet = assignments
            .Select(assignment => assignment.AssignmentId)
            .ToHashSet();
        var scopedSolutions = await BuildScopedCourseSolutionsAsync(siteId, courseId, assignmentIdSet, singleUserId: userId);
        var totalAttempts = scopedSolutions.Count;
        var totalSolved = scopedSolutions
            .Where(solution => solution.Result == AcceptedResultCode)
            .Select(solution => solution.ProblemId)
            .Distinct()
            .Count();
        var totalScore = totalSolved * 100m;
        var progressByAssignment = scopedSolutions
            .GroupBy(solution => solution.AssignmentId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Attempts = group.Count(),
                    Accepted = group.Count(solution => solution.Result == AcceptedResultCode),
                    Solved = group.Where(solution => solution.Result == AcceptedResultCode)
                        .Select(solution => solution.ProblemId)
                        .Distinct()
                        .Count()
                });

        return new AcademicStudentProgress
        {
            UserId = userId,
            Nick = nick.TryGetValue(userId, out var nickValue) ? nickValue : userId,
            TotalScore = totalScore,
            TotalAttempts = totalAttempts,
            TotalSolved = totalSolved,
            Assignments = assignments
                .Select(assignment =>
                {
                    progressByAssignment.TryGetValue(assignment.AssignmentId, out var stats);
                    return new AcademicStudentAssignmentProgress
                    {
                        AssignmentId = assignment.AssignmentId,
                        Title = assignment.Title,
                        Solved = stats?.Solved ?? 0,
                        Attempts = stats?.Attempts ?? 0,
                        Accepted = stats?.Accepted ?? 0
                    };
                })
                .ToList(),
            Techniques = new List<AcademicTechniqueProgress>()
        };
    }

    private async Task<List<AcademicCourseAssignment>> BuildCourseAssignmentsAsync(int siteId, long courseId, string currentUserId)
    {
        var assignments = await _academicContext.CourseAssignments
            .Where(assignment => assignment.CourseId == courseId)
            .OrderBy(assignment => assignment.OpensAt)
            .ThenBy(assignment => assignment.DueAt)
            .ThenBy(assignment => assignment.AssignmentId)
            .ToListAsync();

        if (assignments.Count == 0)
        {
            return new List<AcademicCourseAssignment>();
        }

        var assignmentIds = assignments.Select(assignment => assignment.AssignmentId).ToList();
        var assignmentIdSet = assignmentIds.ToHashSet();
        var assignmentProblems = await _academicContext.CourseAssignmentProblems
            .Where(problem => assignmentIds.Contains(problem.AssignmentId))
            .OrderBy(problem => problem.AssignmentId)
            .ThenBy(problem => problem.ProblemId)
            .ToListAsync();
        var problemIds = assignmentProblems
            .Select(problem => problem.ProblemId)
            .Distinct()
            .ToList();
        var problemTitleMap = problemIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Problems
                .Where(problem => problem.ProblemId.HasValue && problemIds.Contains(problem.ProblemId.Value))
                .Select(problem => new
                {
                    ProblemId = problem.ProblemId!.Value,
                    problem.Title
                })
                .ToDictionaryAsync(problem => problem.ProblemId, problem => problem.Title);

        var studentUserIds = await _academicContext.CourseUsers
            .Where(member => member.CourseId == courseId && member.Role == CourseRoleNames.Student)
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync();
        var allStudentSolutions = studentUserIds.Count == 0
            ? new List<CourseScopedSolutionRow>()
            : await BuildScopedCourseSolutionsAsync(siteId, courseId, assignmentIdSet, userIds: studentUserIds);
        var totalSubmissionsByAssignmentProblem = allStudentSolutions
            .GroupBy(solution => new
            {
                solution.AssignmentId,
                solution.ProblemId
            })
            .ToDictionary(group => (group.Key.AssignmentId, group.Key.ProblemId), group => group.Count());
        var currentUserSolutions = string.IsNullOrWhiteSpace(currentUserId)
            ? new List<CourseScopedSolutionRow>()
            : await BuildScopedCourseSolutionsAsync(siteId, courseId, assignmentIdSet, singleUserId: currentUserId);
        var assignmentStatsByCurrentUser = currentUserSolutions
            .GroupBy(solution => solution.AssignmentId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Attempts = group.Count(),
                    Accepted = group.Count(solution => solution.Result == AcceptedResultCode),
                    Solved = group.Where(solution => solution.Result == AcceptedResultCode)
                        .Select(solution => solution.ProblemId)
                        .Distinct()
                        .Count()
                });
        var solvedProblemsByAssignment = currentUserSolutions
            .Where(solution => solution.Result == AcceptedResultCode)
            .Select(solution => (solution.AssignmentId, solution.ProblemId))
            .ToHashSet();
        var attemptsByAssignmentProblem = currentUserSolutions
            .GroupBy(solution => new
            {
                solution.AssignmentId,
                solution.ProblemId
            })
            .ToDictionary(group => (group.Key.AssignmentId, group.Key.ProblemId), group => group.Count());
        var now = DateTime.Now;

        return assignments
            .Select(assignment =>
            {
                var assignmentProblemRows = assignmentProblems
                    .Where(problem => problem.AssignmentId == assignment.AssignmentId)
                    .ToList();
                var (statusKey, statusLabel) = BuildAssignmentStatus(assignment, now);
                assignmentStatsByCurrentUser.TryGetValue(assignment.AssignmentId, out var currentUserStats);

                return new AcademicCourseAssignment
                {
                    AssignmentId = assignment.AssignmentId,
                    CourseId = courseId,
                    Title = assignment.Title,
                    Description = assignment.Description,
                    OpensAt = assignment.OpensAt,
                    DueAt = assignment.DueAt,
                    LateDueAt = assignment.LateDueAt,
                    IsActive = assignment.IsActive,
                    StatusKey = statusKey,
                    StatusLabel = statusLabel,
                    ProblemCount = assignmentProblemRows.Count(problem => problem.IsVisible),
                    AttemptsByCurrentUser = currentUserStats?.Attempts ?? 0,
                    SolvedByCurrentUser = currentUserStats?.Solved ?? 0,
                    AcceptedByCurrentUser = currentUserStats?.Accepted ?? 0,
                    Problems = assignmentProblemRows
                        .Where(problem => problem.IsVisible)
                        .Select(problem => new AcademicCourseAssignmentProblem
                        {
                            ProblemId = problem.ProblemId,
                            Title = problemTitleMap.TryGetValue(problem.ProblemId, out var title)
                                ? title
                                : $"Problema {problem.ProblemId}",
                            Points = problem.Points,
                            IsVisible = problem.IsVisible,
                            IsSolvedByCurrentUser = solvedProblemsByAssignment.Contains((assignment.AssignmentId, problem.ProblemId)),
                            AttemptsByCurrentUser = attemptsByAssignmentProblem.TryGetValue((assignment.AssignmentId, problem.ProblemId), out var attempts)
                                ? attempts
                                : 0,
                            TotalSubmissions = totalSubmissionsByAssignmentProblem.TryGetValue((assignment.AssignmentId, problem.ProblemId), out var totalSubmissions)
                                ? totalSubmissions
                                : 0
                        })
                        .ToList()
                };
            })
            .ToList();
    }

    private async Task<AcademicCourseReportResponse> BuildCourseReportAsync(int siteId, long courseId, bool includeOwner)
    {
        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Curso no encontrado.");
        }

        var ownerUserId = includeOwner
            ? await _academicContext.CourseUsers
                .Where(member => member.CourseId == courseId
                    && member.Role == CourseRoleNames.Teacher)
                .OrderBy(member => member.UserId)
                .Select(member => member.UserId)
                .FirstOrDefaultAsync() ?? course.CreatedByUserId ?? string.Empty
            : string.Empty;
        var studentUserIds = await _academicContext.CourseUsers
            .Where(member => member.CourseId == courseId
                && member.Role == CourseRoleNames.Student)
            .OrderBy(member => member.UserId)
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync();
        var assignments = await _academicContext.CourseAssignments
            .Where(assignment => assignment.CourseId == courseId)
            .OrderBy(assignment => assignment.OpensAt)
            .ThenBy(assignment => assignment.DueAt)
            .ThenBy(assignment => assignment.AssignmentId)
            .ToListAsync();
        var assignmentIds = assignments
            .Select(assignment => assignment.AssignmentId)
            .ToHashSet();
        var scopedSolutions = studentUserIds.Count == 0
            ? new List<CourseScopedSolutionRow>()
            : await BuildScopedCourseSolutionsAsync(siteId, courseId, assignmentIds, userIds: studentUserIds);
        var solutionsByUser = scopedSolutions
            .GroupBy(solution => solution.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var nickMap = await ResolveNickMapAsync(siteId, studentUserIds);
        var rows = studentUserIds
            .Select(userId =>
            {
                var userSolutions = solutionsByUser.TryGetValue(userId, out var rowsByUser)
                    ? rowsByUser
                    : new List<CourseScopedSolutionRow>();

                return new AcademicCourseReportItem
                {
                    UserId = userId,
                    Nick = nickMap.TryGetValue(userId, out var nick) ? nick : userId,
                    TotalSolved = userSolutions
                        .Where(solution => solution.Result == AcceptedResultCode)
                        .Select(solution => solution.ProblemId)
                        .Distinct()
                        .Count(),
                    TotalAttempts = userSolutions.Count,
                    TotalAccepted = userSolutions.Count(solution => solution.Result == AcceptedResultCode),
                    Assignments = assignments
                        .Select(assignment =>
                        {
                            var assignmentSolutions = userSolutions
                                .Where(solution => solution.AssignmentId == assignment.AssignmentId)
                                .ToList();

                            return new AcademicCourseReportAssignmentCell
                            {
                                AssignmentId = assignment.AssignmentId,
                                Solved = assignmentSolutions
                                    .Where(solution => solution.Result == AcceptedResultCode)
                                    .Select(solution => solution.ProblemId)
                                    .Distinct()
                                    .Count(),
                                Attempts = assignmentSolutions.Count,
                                Accepted = assignmentSolutions.Count(solution => solution.Result == AcceptedResultCode)
                            };
                        })
                        .ToList()
                };
            })
            .OrderByDescending(item => item.TotalSolved)
            .ThenByDescending(item => item.TotalAccepted)
            .ThenByDescending(item => item.TotalAttempts)
            .ThenBy(item => item.UserId)
            .ToList();

        for (var index = 0; index < rows.Count; index++)
        {
            rows[index].Rank = index + 1;
        }

        var assignmentProblemCounts = assignments.Count == 0
            ? new Dictionary<long, int>()
            : await _academicContext.CourseAssignmentProblems
                .Where(problem => assignments.Select(assignment => assignment.AssignmentId).Contains(problem.AssignmentId)
                    && problem.IsVisible)
                .GroupBy(problem => problem.AssignmentId)
                .Select(group => new { AssignmentId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.AssignmentId, item => item.Count);

        return new AcademicCourseReportResponse
        {
            CourseId = courseId,
            CourseName = course.Name,
            OwnerUserId = ownerUserId,
            GeneratedAtUtc = DateTime.Now,
            CanDownloadCsv = false,
            StudentCount = studentUserIds.Count,
            Assignments = assignments
                .Select(assignment => new AcademicCourseReportAssignmentColumn
                {
                    AssignmentId = assignment.AssignmentId,
                    Title = assignment.Title,
                    ProblemCount = assignmentProblemCounts.TryGetValue(assignment.AssignmentId, out var problemCount) ? problemCount : 0,
                    OpensAt = assignment.OpensAt,
                    DueAt = assignment.DueAt
                })
                .ToList(),
            Items = rows
        };
    }

    private async Task<List<CourseScopedSolutionRow>> BuildScopedCourseSolutionsAsync(
        int siteId,
        long courseId,
        IReadOnlyCollection<long> assignmentIds,
        string? singleUserId = null,
        IReadOnlyCollection<string>? userIds = null)
    {
        var submissionContextQuery = _academicContext.CourseSubmissionContexts
            .Where(submissionContext => submissionContext.CourseId == courseId);

        if (assignmentIds.Count > 0)
        {
            submissionContextQuery = submissionContextQuery
                .Where(submissionContext => assignmentIds.Contains(submissionContext.AssignmentId));
        }

        if (!string.IsNullOrWhiteSpace(singleUserId))
        {
            submissionContextQuery = submissionContextQuery
                .Where(submissionContext => submissionContext.UserId == singleUserId);
        }
        else if (userIds is { Count: > 0 })
        {
            submissionContextQuery = submissionContextQuery
                .Where(submissionContext => userIds.Contains(submissionContext.UserId));
        }

        var submissionContexts = await submissionContextQuery
            .Select(submissionContext => new
            {
                submissionContext.SolutionId,
                submissionContext.UserId,
                submissionContext.CourseId,
                submissionContext.AssignmentId
            })
            .ToListAsync();

        if (submissionContexts.Count == 0)
        {
            return new List<CourseScopedSolutionRow>();
        }

        var solutionIds = submissionContexts
            .Select(submissionContext => Convert.ToInt32(submissionContext.SolutionId, CultureInfo.InvariantCulture))
            .Distinct()
            .ToList();

        var solutions = await _context.Solutions
            .Where(solution => solution.SiteId == siteId
                && solutionIds.Contains(solution.SolutionId))
            .Select(solution => new
            {
                solution.SolutionId,
                solution.ProblemId,
                solution.Result
            })
            .ToListAsync();

        if (solutions.Count == 0)
        {
            return new List<CourseScopedSolutionRow>();
        }

        var solutionMap = solutions.ToDictionary(solution => solution.SolutionId);

        return submissionContexts
            .Select(submissionContext =>
            {
                var numericSolutionId = Convert.ToInt32(submissionContext.SolutionId, CultureInfo.InvariantCulture);
                return solutionMap.TryGetValue(numericSolutionId, out var solution)
                    ? new CourseScopedSolutionRow
                    {
                        SolutionId = solution.SolutionId,
                        UserId = submissionContext.UserId,
                        ProblemId = solution.ProblemId,
                        Result = solution.Result,
                        CourseId = submissionContext.CourseId,
                        AssignmentId = submissionContext.AssignmentId
                    }
                    : null;
            })
            .Where(solution => solution != null)
            .Select(solution => solution!)
            .ToList();
    }

    private async Task<Dictionary<string, string>> ResolveNickMapAsync(int siteId, IEnumerable<string> userIds)
    {
        var profileMap = await LoadUserProfilesByUserIdAsync(siteId, userIds);
        return profileMap.ToDictionary(
            profile => profile.Key,
            profile => string.IsNullOrWhiteSpace(profile.Value.Nick) ? profile.Key : profile.Value.Nick,
            StringComparer.OrdinalIgnoreCase);
    }

    private static (string StatusKey, string StatusLabel) BuildAssignmentStatus(DbCourseAssignment assignment, DateTime now)
    {
        if (!assignment.IsActive)
        {
            return ("inactive", "Inactiva");
        }

        if (now < assignment.OpensAt)
        {
            return ("upcoming", "Proxima");
        }

        var closesAt = assignment.LateDueAt ?? assignment.DueAt;
        if (now <= closesAt)
        {
            return ("active", "Activa");
        }

        return ("finished", "Finalizada");
    }

    private static void EnsureAssignmentAcceptsSubmissions(DbCourseAssignment assignment, DateTime now)
    {
        var (statusKey, _) = BuildAssignmentStatus(assignment, now);

        switch (statusKey)
        {
            case "inactive":
                throw new InvalidOperationException("Esta tarea está inactiva y no acepta envíos.");
            case "upcoming":
                throw new InvalidOperationException("Esta tarea todavía no acepta envíos.");
            case "finished":
                throw new InvalidOperationException("Esta tarea ya finalizó y no acepta envíos.");
        }
    }
}
