using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class AcademicService : IAcademicService
{
    private readonly IAcademicRepository _academicRepository;

    public AcademicService(IAcademicRepository academicRepository)
    {
        _academicRepository = academicRepository ?? throw new ArgumentNullException(nameof(academicRepository));
    }

    public async Task<IEnumerable<AcademicInstitution>> GetInstitutionsAsync(int siteId, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        return await _academicRepository.GetInstitutionsAsync(siteId);
    }

    public async Task<IEnumerable<AcademicInstitutionRankingItem>> GetInstitutionsRankingAsync(int siteId, CurrentUser currentUser, int limit)
    {
        ValidateSite(siteId, currentUser.SiteId);
        return await _academicRepository.GetInstitutionsRankingAsync(siteId, limit);
    }

    public async Task<IEnumerable<AcademicCourseSummary>> GetMyCoursesAsync(int siteId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);
        return await _academicRepository.GetMyCoursesAsync(siteId, currentUser.UserId);
    }

    public async Task<IEnumerable<AcademicCourseSummary>> GetManageableCoursesAsync(int siteId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        var canManageAcademic = IsAcademicManager(currentUser);
        var courses = (await _academicRepository.GetManageableCoursesAsync(siteId, currentUser.UserId, canManageAcademic)).ToList();

        if (canManageAcademic && currentUser.Role != UserRolesEnum.Administrador)
        {
            var managerRole = GetAcademicManagerCourseRole(currentUser);
            foreach (var course in courses.Where(course => string.Equals(course.Role, "admin", StringComparison.OrdinalIgnoreCase)))
            {
                course.Role = managerRole;
            }
        }

        return courses;
    }

    public async Task<AcademicCourseDetail> CreateCourseAsync(int siteId, CurrentUser currentUser, AcademicCourseCreationRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (!IsAcademicManager(currentUser))
        {
            throw new UnauthorizedAccessException("Only administrators, teachers or assistants can create courses.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Course name is required.");
        }

        return await _academicRepository.CreateCourseAsync(siteId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseDetail> JoinCourseAsync(int siteId, CurrentUser currentUser, AcademicJoinCourseRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(request.InviteCode))
        {
            throw new ArgumentException("Invite code is required.");
        }

        return await _academicRepository.JoinCourseAsync(siteId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseDetail> GetCourseAsync(int siteId, long courseId, CurrentUser currentUser)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        if (IsAcademicManager(currentUser))
        {
            course.CanManage = true;
            if (string.IsNullOrWhiteSpace(course.MemberRole))
            {
                course.MemberRole = GetAcademicManagerCourseRole(currentUser);
            }
        }

        return course;
    }

    public async Task<IEnumerable<AcademicCourseMember>> GetCourseMembersAsync(int siteId, long courseId, CurrentUser currentUser)
    {
        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureAcademicManager(currentUser, "view course members");
        return await _academicRepository.GetCourseMembersAsync(siteId, courseId);
    }

    public async Task<AcademicCourseMember> AddCourseMemberAsync(
        int siteId,
        long courseId,
        CurrentUser currentUser,
        AcademicCourseMemberCreationRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("Member username is required.");
        }

        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureAcademicManager(currentUser, "add course members");
        request.UserId = request.UserId.Trim();
        request.Role = string.Equals(request.Role?.Trim(), "assistant", StringComparison.OrdinalIgnoreCase)
            ? "assistant"
            : "student";

        return await _academicRepository.AddCourseMemberAsync(siteId, courseId, request);
    }

    public async Task RemoveCourseMemberAsync(int siteId, long courseId, string memberUserId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException("Member username is required.");
        }

        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureAcademicManager(currentUser, "remove course members");
        await _academicRepository.RemoveCourseMemberAsync(siteId, courseId, memberUserId.Trim());
    }

    public async Task<AcademicCourseAssignment> CreateCourseAssignmentAsync(
        int siteId,
        long courseId,
        CurrentUser currentUser,
        AcademicCourseAssignmentCreationRequest request)
    {
        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureAcademicManager(currentUser, "create assignments");
        EnsureValidAssignmentRequest(request);
        return await _academicRepository.CreateCourseAssignmentAsync(siteId, courseId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseAssignment> UpdateCourseAssignmentAsync(
        int siteId,
        long courseId,
        long assignmentId,
        CurrentUser currentUser,
        AcademicCourseAssignmentCreationRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (assignmentId <= 0)
        {
            throw new ArgumentException("Assignment id is required.");
        }

        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureAcademicManager(currentUser, "update assignments");
        EnsureValidAssignmentRequest(request);
        return await _academicRepository.UpdateCourseAssignmentAsync(siteId, courseId, assignmentId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseAssignmentDetailResponse> GetCourseAssignmentAsync(int siteId, long courseId, long assignmentId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (assignmentId <= 0)
        {
            throw new ArgumentException("Assignment id is required.");
        }

        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        if (IsAcademicManager(currentUser) && string.IsNullOrWhiteSpace(course.MemberRole))
        {
            course.MemberRole = GetAcademicManagerCourseRole(currentUser);
        }

        var assignment = await _academicRepository.GetCourseAssignmentAsync(siteId, courseId, assignmentId, currentUser.UserId);
        assignment.MemberRole = course.MemberRole;
        assignment.CanManage = course.CanManage || IsAcademicManager(currentUser);
        return assignment;
    }

    public async Task<PublicSubmissionsResponse> GetCourseAssignmentSubmissionsAsync(
        int siteId,
        long courseId,
        long assignmentId,
        int page,
        int pageSize,
        CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (assignmentId <= 0)
        {
            throw new ArgumentException("Assignment id is required.");
        }

        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        return await _academicRepository.GetCourseAssignmentSubmissionsAsync(siteId, courseId, assignmentId, Math.Max(page, 1), Clamp(pageSize, 1, 100));
    }

    public async Task<IEnumerable<AcademicCourseRankingItem>> GetCourseRankingAsync(int siteId, long courseId, CurrentUser currentUser)
    {
        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        return await _academicRepository.GetCourseRankingAsync(siteId, courseId);
    }

    public async Task<AcademicCourseReportResponse> GetCourseReportAsync(int siteId, long courseId, CurrentUser currentUser)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        var report = await _academicRepository.GetCourseReportAsync(siteId, courseId);
        report.CanDownloadCsv = IsAcademicManager(currentUser);
        return report;
    }

    public async Task<AcademicStudentProgress> GetStudentProgressAsync(int siteId, long courseId, string studentUserId, CurrentUser currentUser)
    {
        await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        if (!IsAcademicManager(currentUser)
            && !string.Equals(studentUserId, currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only teachers, assistants or owner student can view this progress.");
        }

        return await _academicRepository.GetStudentProgressAsync(siteId, courseId, studentUserId);
    }

    public async Task<IEnumerable<LearningPathTrackSummary>> GetLearningPathsAsync(int siteId, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        return await _academicRepository.GetLearningPathsAsync(siteId);
    }

    public async Task<LearningPathResponse> GetLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("Learning path key is required.");
        }

        return await _academicRepository.GetLearningPathAsync(siteId, learningPathKey);
    }

    public async Task<LearningPathProgressResponse> GetLearningPathProgressAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("Learning path key is required.");
        }

        return await _academicRepository.GetLearningPathProgressAsync(siteId, learningPathKey, currentUser.UserId);
    }

    public async Task<LearningPathProgressResponse> SaveLearningPathProgressAsync(
        int siteId,
        string learningPathKey,
        CurrentUser currentUser,
        LearningPathProgressUpdateRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("Learning path key is required.");
        }

        return await _academicRepository.SaveLearningPathProgressAsync(
            siteId,
            learningPathKey,
            currentUser.UserId,
            request ?? new LearningPathProgressUpdateRequest());
    }

    public async Task<AcademicSubmissionResponse> SubmitAsync(CurrentUser currentUser, AcademicSubmissionRequest request)
    {
        ValidateUser(currentUser.UserId);

        if (request.ProblemId <= 0)
        {
            throw new ArgumentException("ProblemId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            throw new ArgumentException("SourceCode is required.");
        }

        return await _academicRepository.SubmitAsync(currentUser.SiteId, currentUser.UserId, request);
    }

    private static void ValidateSite(int siteIdFromRoute, int siteIdFromToken)
    {
        if (siteIdFromRoute != siteIdFromToken)
        {
            throw new UnauthorizedAccessException("Cross-site access is forbidden.");
        }
    }

    private static void ValidateUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.Equals(userId, "defaultUserId", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid user context.");
        }
    }

    private static bool IsAcademicManager(CurrentUser currentUser)
    {
        return currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar;
    }

    private static void ValidateRequestContext(int siteId, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        ValidateUser(currentUser.UserId);
    }

    private async Task<AcademicCourseDetail> GetCourseForCurrentUserAsync(int siteId, long courseId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);
        return await _academicRepository.GetCourseAsync(siteId, courseId, currentUser.UserId, IsAcademicManager(currentUser));
    }

    private static void EnsureAcademicManager(CurrentUser currentUser, string action)
    {
        if (IsAcademicManager(currentUser))
        {
            return;
        }

        throw new UnauthorizedAccessException($"Only administrators, teachers or assistants can {action}.");
    }

    private static void EnsureValidAssignmentRequest(AcademicCourseAssignmentCreationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Assignment title is required.");
        }

        var problemIds = request.ProblemIds
            .Where(problemId => problemId > 0)
            .Distinct()
            .ToList();

        if (problemIds.Count == 0)
        {
            throw new ArgumentException("At least one problem is required.");
        }

        request.ProblemIds = problemIds;
        request.OpensAt ??= DateTime.UtcNow;
        request.DueAt ??= request.OpensAt.Value.AddDays(7);

        if (request.DueAt.Value < request.OpensAt.Value)
        {
            throw new ArgumentException("Due date must be after open date.");
        }

        if (request.LateDueAt.HasValue && request.LateDueAt.Value < request.DueAt.Value)
        {
            throw new ArgumentException("Late due date must be after due date.");
        }
    }

    private static string GetAcademicManagerCourseRole(CurrentUser currentUser)
    {
        return currentUser.Role switch
        {
            UserRolesEnum.Administrador => "admin",
            UserRolesEnum.Auxiliar => "assistant",
            UserRolesEnum.Docente => "teacher",
            _ => string.Empty
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
