using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class AcademicRepository : IAcademicRepository
{
    private const short AcceptedResultCode = 4;

    private sealed class CourseScopedSolutionRow
    {
        public int SolutionId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int ProblemId { get; set; }

        public short Result { get; set; }

        public long CourseId { get; set; }

        public long AssignmentId { get; set; }
    }

    private sealed class LearningPathProgressContext
    {
        public long LearningPathId { get; init; }

        public string LearningPathKey { get; init; } = string.Empty;

        public List<string> OrderedTopicIds { get; init; } = new();

        public HashSet<string> ValidTopicIdSet { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly AppDbContext _context;
    private readonly AcademicCatalogDbContext _academicContext;

    public AcademicRepository(AppDbContext context, AcademicCatalogDbContext academicContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _academicContext = academicContext ?? throw new ArgumentNullException(nameof(academicContext));
    }

    public Task<IEnumerable<AcademicInstitution>> GetInstitutionsAsync(int siteId)
    {
        return Task.FromResult<IEnumerable<AcademicInstitution>>(Array.Empty<AcademicInstitution>());
    }

    public Task<IEnumerable<AcademicInstitutionRankingItem>> GetInstitutionsRankingAsync(int siteId, int limit)
    {
        return Task.FromResult<IEnumerable<AcademicInstitutionRankingItem>>(Array.Empty<AcademicInstitutionRankingItem>());
    }

    public async Task<IEnumerable<AcademicCourseSummary>> GetMyCoursesAsync(int siteId, string userId)
    {
        var myMemberships = await _academicContext.CourseUsers
            .Where(member => member.UserId == userId)
            .Select(member => new
            {
                member.CourseId,
                member.Role
            })
            .ToListAsync();

        if (myMemberships.Count == 0)
        {
            return Array.Empty<AcademicCourseSummary>();
        }

        var courseIds = myMemberships.Select(item => item.CourseId).Distinct().ToList();
        var membershipRolesByCourse = myMemberships
            .ToDictionary(item => item.CourseId, item => item.Role);

        return await BuildCourseSummariesAsync(courseIds, membershipRolesByCourse, CourseRoleNames.Student, userId);
    }

    public async Task<IEnumerable<AcademicCourseSummary>> GetManageableCoursesAsync(int siteId, string userId, bool includeAllCourses)
    {
        var managedMemberships = await _academicContext.CourseUsers
            .Where(member => member.UserId == userId
                && (member.Role == CourseRoleNames.Teacher || member.Role == CourseRoleNames.Assistant || member.Role == CourseRoleNames.Admin))
            .Select(member => new { member.CourseId, member.Role })
            .ToListAsync();

        var managedMembershipIds = managedMemberships.Select(member => member.CourseId).ToList();
        var courseIds = includeAllCourses
            ? await _academicContext.Courses
                .OrderBy(course => course.Name)
                .ThenBy(course => course.CourseId)
                .Select(course => course.CourseId)
                .ToListAsync()
            : await _academicContext.Courses
                .Where(course => course.CreatedByUserId == userId || managedMembershipIds.Contains(course.CourseId))
                .OrderBy(course => course.Name)
                .ThenBy(course => course.CourseId)
                .Select(course => course.CourseId)
                .ToListAsync();

        if (courseIds.Count == 0)
        {
            return Array.Empty<AcademicCourseSummary>();
        }

        var membershipRolesByCourse = managedMemberships
            .Where(member => courseIds.Contains(member.CourseId))
            .GroupBy(member => member.CourseId)
            .ToDictionary(group => group.Key, group => group.First().Role);

        return await BuildCourseSummariesAsync(courseIds, membershipRolesByCourse, includeAllCourses ? CourseRoleNames.Admin : CourseRoleNames.Teacher, userId);
    }

    public async Task<AcademicCourseDetail> CreateCourseAsync(int siteId, string userId, AcademicCourseCreationRequest request)
    {
        var courseKey = await GenerateUniqueCourseKeyAsync(request.Name);
        var inviteCode = await GenerateUniqueInviteCodeAsync();

        var course = new DbCourse
        {
            CourseKey = courseKey,
            InviteCode = inviteCode,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedByUserId = userId
        };

        await _academicContext.Courses.AddAsync(course);
        await _academicContext.SaveChangesAsync();

        await _academicContext.CourseUsers.AddAsync(new DbCourseUser
        {
            CourseId = course.CourseId,
            UserId = userId,
            Role = CourseRoleNames.Teacher
        });

        await _academicContext.SaveChangesAsync();

        return await GetCourseAsync(siteId, course.CourseId, userId, allowAdminAccess: false);
    }

    public async Task<AcademicCourseDetail> JoinCourseAsync(int siteId, string userId, AcademicJoinCourseRequest request)
    {
        var code = request.InviteCode.Trim().ToUpperInvariant();

        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.InviteCode == code);

        if (course == null)
        {
            throw new ArgumentException("Invite code is invalid.");
        }

        var existingMember = await _academicContext.CourseUsers
            .FirstOrDefaultAsync(member => member.CourseId == course.CourseId
                && member.UserId == userId);

        if (existingMember == null)
        {
            await _academicContext.CourseUsers.AddAsync(new DbCourseUser
            {
                CourseId = course.CourseId,
                UserId = userId,
                Role = CourseRoleNames.Student
            });
        }

        await _academicContext.SaveChangesAsync();

        return await GetCourseAsync(siteId, course.CourseId, userId, allowAdminAccess: false);
    }

    public async Task<AcademicCourseDetail> GetCourseAsync(int siteId, long courseId, string userId, bool allowAdminAccess)
    {
        var member = await _academicContext.CourseUsers
            .FirstOrDefaultAsync(courseMember => courseMember.CourseId == courseId
                && courseMember.UserId == userId);

        if (member == null && !allowAdminAccess)
        {
            throw new UnauthorizedAccessException("User is not a member of this course.");
        }

        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Course not found.");
        }

        var teacher = await _academicContext.CourseUsers
            .Where(courseMember => courseMember.CourseId == courseId
                && courseMember.Role == CourseRoleNames.Teacher)
            .OrderBy(courseMember => courseMember.UserId)
            .Select(courseMember => courseMember.UserId)
            .FirstOrDefaultAsync();

        var studentCount = await _academicContext.CourseUsers
            .Where(courseMember => courseMember.CourseId == courseId
                && courseMember.Role == CourseRoleNames.Student)
            .CountAsync();
        var assignments = await BuildCourseAssignmentsAsync(siteId, courseId, userId);
        await EnsureAssignmentContentItemsAsync(courseId, teacher ?? course.CreatedByUserId ?? userId);
        var canViewDraftContent = allowAdminAccess || (member != null && member.Role != CourseRoleNames.Student);
        var storedContent = await _academicContext.CourseContentItems
            .Where(item => item.CourseId == courseId && (item.IsPublished || canViewDraftContent))
            .OrderBy(item => item.Position)
            .ThenBy(item => item.ItemId)
            .ToListAsync();
        var assignmentsById = assignments.ToDictionary(item => item.AssignmentId);
        var content = storedContent.Select(item => new AcademicCourseContentItem
        {
            ItemId = item.ItemId,
            CourseId = item.CourseId,
            Type = item.ItemType,
            Title = item.Title,
            Description = item.Description,
            ContentUrl = item.ContentUrl,
            ContentBody = item.ContentBody,
            AssignmentId = item.AssignmentId,
            Position = item.Position,
            IsPublished = item.IsPublished,
            Assignment = item.AssignmentId.HasValue && assignmentsById.TryGetValue(item.AssignmentId.Value, out var linkedAssignment) ? linkedAssignment : null
        }).ToList();
        var linkedAssignmentIds = content.Where(item => item.AssignmentId.HasValue).Select(item => item.AssignmentId!.Value).ToHashSet();
        var nextPosition = content.Count == 0 ? 10 : content.Max(item => item.Position) + 10;
        foreach (var assignment in assignments.Where(item => !linkedAssignmentIds.Contains(item.AssignmentId)))
        {
            content.Add(new AcademicCourseContentItem
            {
                CourseId = courseId,
                Type = "contest",
                Title = assignment.Title,
                Description = assignment.Description,
                AssignmentId = assignment.AssignmentId,
                Position = nextPosition,
                IsPublished = assignment.IsActive,
                Assignment = assignment
            });
            nextPosition += 10;
        }
        var effectiveMemberRole = member?.Role ?? (allowAdminAccess ? CourseRoleNames.Admin : string.Empty);
        var canManage = string.Equals(effectiveMemberRole, CourseRoleNames.Teacher, StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectiveMemberRole, CourseRoleNames.Assistant, StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectiveMemberRole, CourseRoleNames.Admin, StringComparison.OrdinalIgnoreCase);
        var ownerUserId = teacher ?? course.CreatedByUserId ?? userId;
        var canSeeInviteCode = string.Equals(ownerUserId, userId, StringComparison.OrdinalIgnoreCase);

        return new AcademicCourseDetail
        {
            CourseId = course.CourseId,
            SiteId = siteId,
            Name = course.Name,
            Description = course.Description,
            InstitutionId = null,
            InviteCode = canSeeInviteCode ? course.InviteCode : string.Empty,
            OwnerUserId = ownerUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = course.CreatedByUserId ?? teacher ?? userId,
            IsActive = true,
            MemberRole = effectiveMemberRole,
            StudentCount = studentCount,
            AssignmentCount = assignments.Count,
            CanManage = canManage,
            Assignments = assignments,
            Content = content.OrderBy(item => item.Position).ToList()
        };
    }

    public async Task<IEnumerable<AcademicCourseMember>> GetCourseMembersAsync(int siteId, long courseId)
    {
        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Course not found.");
        }

        var members = await _academicContext.CourseUsers
            .Where(member => member.CourseId == courseId)
            .ToListAsync();

        if (members.Count == 0)
        {
            return Array.Empty<AcademicCourseMember>();
        }

        var ownerUserId = members
            .Where(member => member.Role == CourseRoleNames.Teacher)
            .OrderBy(member => member.UserId)
            .Select(member => member.UserId)
            .FirstOrDefault() ?? course.CreatedByUserId ?? string.Empty;
        var userIds = members.Select(member => member.UserId).Distinct().ToList();
        var profileMap = await LoadUserProfilesByUserIdAsync(siteId, userIds);

        return members
            .Select(member =>
            {
                profileMap.TryGetValue(member.UserId, out var profile);
                return new AcademicCourseMember
                {
                    UserId = member.UserId,
                    Nick = profile?.Nick ?? member.UserId,
                    Lastname = profile?.Lastname,
                    Email = profile?.Email,
                    Role = member.Role,
                    IsOwner = string.Equals(member.UserId, ownerUserId, StringComparison.OrdinalIgnoreCase)
                };
            })
            .OrderBy(member => SortCourseRole(member.Role))
            .ThenBy(member => member.UserId)
            .ToList();
    }

    public async Task<AcademicCourseMember> AddCourseMemberAsync(int siteId, long courseId, AcademicCourseMemberCreationRequest request)
    {
        var course = await _academicContext.Courses
            .FirstOrDefaultAsync(item => item.CourseId == courseId);

        if (course == null)
        {
            throw new ArgumentException("Course not found.");
        }

        var normalizedUserId = request.UserId.Trim();
        var normalizedRole = request.Role.Trim().ToLowerInvariant();
        var userInSite = await _context.Users
            .FirstOrDefaultAsync(user => user.SiteId == siteId
                && user.UserId == normalizedUserId);

        if (userInSite == null)
        {
            throw new ArgumentException("User not found in this site.");
        }

        if (!userInSite.IsActive || userInSite.IsDeleted)
        {
            throw new ArgumentException("User exists in this site but is inactive or deleted.");
        }

        var existingMember = await _academicContext.CourseUsers
            .FirstOrDefaultAsync(member => member.CourseId == courseId
                && member.UserId == normalizedUserId);

        if (existingMember == null)
        {
            await _academicContext.CourseUsers.AddAsync(new DbCourseUser
            {
                CourseId = courseId,
                UserId = normalizedUserId,
                Role = normalizedRole
            });
        }
        else
        {
            if (string.Equals(existingMember.Role, CourseRoleNames.Teacher, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Teacher membership cannot be edited from this endpoint.");
            }

            existingMember.Role = normalizedRole;
        }

        await _academicContext.SaveChangesAsync();

        return (await GetCourseMembersAsync(siteId, courseId))
            .First(member => string.Equals(member.UserId, normalizedUserId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task RemoveCourseMemberAsync(int siteId, long courseId, string userId)
    {
        var existingMember = await _academicContext.CourseUsers
            .FirstOrDefaultAsync(member => member.CourseId == courseId
                && member.UserId == userId);

        if (existingMember == null)
        {
            throw new ArgumentException("Course member not found.");
        }

        if (string.Equals(existingMember.Role, CourseRoleNames.Teacher, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Teacher membership cannot be removed from this endpoint.");
        }

        _academicContext.CourseUsers.Remove(existingMember);
        await _academicContext.SaveChangesAsync();
    }

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
            throw new ArgumentException("Course not found.");
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
            throw new ArgumentException("Some problems are not available in this site.");
        }

        var assignment = new DbCourseAssignment
        {
            CourseId = courseId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OpensAt = request.OpensAt ?? DateTime.UtcNow,
            DueAt = request.DueAt ?? DateTime.UtcNow.AddDays(7),
            LateDueAt = request.LateDueAt,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
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
            CreatedAt = DateTime.UtcNow
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
            CreatedAt = DateTime.UtcNow
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

    public async Task ReorderCourseContentAsync(long courseId, IReadOnlyList<long> itemIds)
    {
        var items = await _academicContext.CourseContentItems
            .Where(item => item.CourseId == courseId)
            .ToListAsync();
        var itemsById = items.ToDictionary(item => item.ItemId);
        var position = 10;
        foreach (var itemId in itemIds)
        {
            if (!itemsById.TryGetValue(itemId, out var item)) continue;
            item.Position = position;
            position += 10;
        }
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
            throw new ArgumentException("Course not found.");
        }

        var assignment = await _academicContext.CourseAssignments
            .FirstOrDefaultAsync(item => item.AssignmentId == assignmentId
                && item.CourseId == courseId);

        if (assignment == null)
        {
            throw new ArgumentException("Assignment not found in this course.");
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
            throw new ArgumentException("Some problems are not available in this site.");
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
            throw new ArgumentException("Course not found.");
        }

        var assignment = (await BuildCourseAssignmentsAsync(siteId, courseId, currentUserId))
            .FirstOrDefault(item => item.AssignmentId == assignmentId);

        if (assignment == null)
        {
            throw new ArgumentException("Assignment not found.");
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
            GeneratedAtUtc = DateTime.UtcNow,
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
            throw new ArgumentException("Assignment not found.");
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
                UpdatedAtUtc = DateTime.UtcNow,
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
                UpdatedAtUtc = DateTime.UtcNow,
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
            UpdatedAtUtc = DateTime.UtcNow,
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
            throw new ArgumentException("Learning path not found.");
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
            throw new ArgumentException("Learning path key already exists.");

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
            throw new ArgumentException("Learning path key cannot be changed.");
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

        var stageIds = await _academicContext.LearningPathTopics.Where(link => link.LearningPathId == path.LearningPathId).Select(link => link.TopicId).ToListAsync();
        foreach (var stageId in stageIds) await DeleteStageDataAsync(path.LearningPathId, stageId);
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
        if (await _academicContext.Topics.AnyAsync(topic => topic.TopicKey == key)) throw new ArgumentException("Stage key already exists.");
        if (await HasStageWithOrderAsync(path.LearningPathId, request.Order)) throw new ArgumentException("Stage order already exists in this learning path.");
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
            ?? throw new ArgumentException("Stage not found.");
        if (await _academicContext.LearningPathTopics.AnyAsync(link => link.LearningPathId == path.LearningPathId && link.TopicId == stageId))
            throw new ArgumentException("Stage is already linked to this learning path.");
        if (await HasStageWithOrderAsync(path.LearningPathId, stage.SortOrder))
            throw new ArgumentException("Stage order already exists in this learning path.");

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
        if (await _academicContext.Topics.AnyAsync(item => item.TopicId != stageId && item.TopicKey == key)) throw new ArgumentException("Stage key already exists.");
        if (await HasStageWithOrderAsync(path.LearningPathId, request.Order, stageId)) throw new ArgumentException("Stage order already exists in this learning path.");
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
        if (await _academicContext.Subtopics.AnyAsync(item => item.TopicId == stageId && item.SubtopicKey == key)) throw new ArgumentException("Topic key already exists in this stage.");
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
        var topic = await _academicContext.Subtopics.FirstOrDefaultAsync(item => item.SubtopicId == topicId && item.TopicId == stageId) ?? throw new ArgumentException("Topic not found.");
        var key = NormalizeCatalogKey(request.Key);
        if (await _academicContext.Subtopics.AnyAsync(item => item.SubtopicId != topicId && item.TopicId == stageId && item.SubtopicKey == key)) throw new ArgumentException("Topic key already exists in this stage.");
        var previousKey = topic.SubtopicKey;
        topic.SubtopicKey = key; ApplyTopic(topic, request);
        if (!string.Equals(previousKey, key, StringComparison.OrdinalIgnoreCase))
        {
            var topicProgress = await _academicContext.LearningPathTopicProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.TopicId == previousKey).ToListAsync();
            topicProgress.ForEach(item => item.TopicId = key);
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
        var topic = await _academicContext.Subtopics.FirstOrDefaultAsync(item => item.SubtopicId == topicId && item.TopicId == stageId) ?? throw new ArgumentException("Topic not found.");
        _academicContext.SubtopicProblems.RemoveRange(_academicContext.SubtopicProblems.Where(item => item.SubtopicId == topicId));
        _academicContext.SubtopicTags.RemoveRange(_academicContext.SubtopicTags.Where(item => item.SubtopicId == topicId));
        _academicContext.LearningPathTopicProgresses.RemoveRange(_academicContext.LearningPathTopicProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.TopicId == topic.SubtopicKey));
        var pathProgress = await _academicContext.LearningPathProgresses.Where(item => item.LearningPathId == path.LearningPathId && item.LastTopicId == topic.SubtopicKey).ToListAsync();
        pathProgress.ForEach(item => item.LastTopicId = null);
        _academicContext.Subtopics.Remove(topic);
        await _academicContext.SaveChangesAsync();
    }

    private async Task<DbLearningPath> FindLearningPathAsync(int siteId, string key) =>
        await _academicContext.LearningPaths.FirstOrDefaultAsync(path => path.SiteId == siteId && path.LearningPathKey == key) ?? throw new ArgumentException("Learning path not found.");

    private async Task EnsureStageLinkedAsync(long pathId, long stageId)
    {
        if (!await _academicContext.LearningPathTopics.AnyAsync(link => link.LearningPathId == pathId && link.TopicId == stageId)) throw new ArgumentException("Stage not found in learning path.");
    }

    private async Task EnsureStageIsExclusiveAsync(long pathId, long stageId)
    {
        if (await _academicContext.LearningPathTopics.AnyAsync(link => link.TopicId == stageId && link.LearningPathId != pathId))
            throw new InvalidOperationException("Shared stages cannot be modified. Remove the stage from the other learning paths first.");
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
        if (validIds.Count != ids.Count) throw new ArgumentException("One or more problems are not active in this site.");
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
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Key contains no valid characters.");
        return normalized;
    }

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
        var now = DateTime.UtcNow;

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
        var now = DateTime.UtcNow;

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
        var now = DateTime.UtcNow;

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
            throw new ArgumentException("Course not found.");
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
            GeneratedAtUtc = DateTime.UtcNow,
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

    private async Task<Dictionary<string, DbUserProfile>> LoadUserProfilesByUserIdAsync(int siteId, IEnumerable<string> userIds)
    {
        var normalizedUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedUserIds.Count == 0)
        {
            return new Dictionary<string, DbUserProfile>(StringComparer.OrdinalIgnoreCase);
        }

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.SiteId == siteId
                && normalizedUserIds.Contains(profile.UserId))
            .ToListAsync();

        return profiles
            .GroupBy(profile => profile.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(profile => !string.IsNullOrWhiteSpace(profile.Nick))
                    .ThenByDescending(profile => !string.IsNullOrWhiteSpace(profile.Email))
                    .First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<AcademicCourseSummary>> BuildCourseSummariesAsync(
        IReadOnlyCollection<long> courseIds,
        IReadOnlyDictionary<long, string> membershipRolesByCourse,
        string fallbackRole,
        string currentUserId)
    {
        var courses = await _academicContext.Courses
            .Where(course => courseIds.Contains(course.CourseId))
            .ToListAsync();

        var studentCounts = await _academicContext.CourseUsers
            .Where(member => member.Role == CourseRoleNames.Student
                && courseIds.Contains(member.CourseId))
            .GroupBy(member => member.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count);

        var assignmentCounts = await _academicContext.CourseAssignments
            .Where(assignment => courseIds.Contains(assignment.CourseId))
            .GroupBy(assignment => assignment.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count);

        var ownerByCourse = await _academicContext.CourseUsers
            .Where(member => courseIds.Contains(member.CourseId)
                && member.Role == CourseRoleNames.Teacher)
            .GroupBy(member => member.CourseId)
            .Select(group => new
            {
                CourseId = group.Key,
                OwnerUserId = group.OrderBy(member => member.UserId)
                    .Select(member => member.UserId)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToDictionaryAsync(item => item.CourseId, item => item.OwnerUserId);

        return courses
            .Select(course =>
            {
                var ownerUserId = ownerByCourse.TryGetValue(course.CourseId, out var resolvedOwnerUserId)
                    ? resolvedOwnerUserId
                    : course.CreatedByUserId ?? string.Empty;
                var canSeeInviteCode = string.Equals(ownerUserId, currentUserId, StringComparison.OrdinalIgnoreCase);

                return new AcademicCourseSummary
                {
                    CourseId = course.CourseId,
                    Name = course.Name,
                    OwnerUserId = ownerUserId,
                    Role = membershipRolesByCourse.TryGetValue(course.CourseId, out var role) ? role : fallbackRole,
                    StudentCount = studentCounts.TryGetValue(course.CourseId, out var students) ? students : 0,
                    AssignmentCount = assignmentCounts.TryGetValue(course.CourseId, out var assignments) ? assignments : 0,
                    InviteCode = canSeeInviteCode ? course.InviteCode : string.Empty
                };
            })
            .OrderByDescending(course => course.CourseId)
            .ThenBy(course => course.CourseId)
            .ToList();
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

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = $"C{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}";

            var exists = await _academicContext.Courses
                .AnyAsync(course => course.InviteCode == code);

            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique invite code.");
    }

    private async Task<string> GenerateUniqueCourseKeyAsync(string courseName)
    {
        var baseKey = BuildCourseKeyBase(courseName);
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            baseKey = "course";
        }

        for (var suffix = 0; suffix < 1000; suffix++)
        {
            var candidate = suffix == 0
                ? baseKey
                : BuildSuffixedCourseKey(baseKey, suffix + 1);

            var exists = await _academicContext.Courses
                .AnyAsync(course => course.CourseKey == candidate);

            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique course key.");
    }

    private static string BuildCourseKeyBase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (builder.Length == 0 || builder[^1] == '_')
            {
                continue;
            }

            builder.Append('_');
        }

        var key = builder
            .ToString()
            .Trim('_');

        if (key.Length <= 64)
        {
            return key;
        }

        return key[..64].TrimEnd('_');
    }

    private static string BuildSuffixedCourseKey(string baseKey, int suffix)
    {
        var suffixText = $"_{suffix}";
        var maxBaseLength = Math.Max(1, 64 - suffixText.Length);
        var trimmedBaseKey = baseKey.Length <= maxBaseLength
            ? baseKey
            : baseKey[..maxBaseLength].TrimEnd('_');

        if (string.IsNullOrWhiteSpace(trimmedBaseKey))
        {
            trimmedBaseKey = "course";
        }

        return $"{trimmedBaseKey}{suffixText}";
    }

    private static int SortCourseRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            CourseRoleNames.Teacher => 0,
            CourseRoleNames.Assistant => 1,
            _ => 2
        };
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
