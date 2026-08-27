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

        return await BuildCourseSummariesAsync(courseIds, membershipRolesByCourse, CourseRoleNames.Student, userId, canManage: false);
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

        return await BuildCourseSummariesAsync(courseIds, membershipRolesByCourse, includeAllCourses ? CourseRoleNames.Admin : CourseRoleNames.Teacher, userId, canManage: true);
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
        var canSeeInviteCode = canManage || string.Equals(ownerUserId, userId, StringComparison.OrdinalIgnoreCase);

        return new AcademicCourseDetail
        {
            CourseId = course.CourseId,
            SiteId = siteId,
            Name = course.Name,
            Description = course.Description,
            InstitutionId = null,
            InviteCode = canSeeInviteCode ? course.InviteCode : string.Empty,
            OwnerUserId = ownerUserId,
            CreatedAt = DateTime.Now,
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
        string currentUserId,
        bool canManage)
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
                var canSeeInviteCode = canManage || string.Equals(ownerUserId, currentUserId, StringComparison.OrdinalIgnoreCase);

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
            .ToList();
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
}
