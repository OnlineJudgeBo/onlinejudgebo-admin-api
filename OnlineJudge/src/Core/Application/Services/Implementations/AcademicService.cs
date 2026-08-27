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

        var includeAllCourses = currentUser.Role == UserRolesEnum.Administrador;
        var courses = (await _academicRepository.GetManageableCoursesAsync(siteId, currentUser.UserId, includeAllCourses)).ToList();

        if (IsAcademicManager(currentUser) && currentUser.Role != UserRolesEnum.Administrador)
        {
            var managerRole = GetAcademicManagerCourseRole(currentUser);
            foreach (var course in courses.Where(course => string.Equals(course.Role, CourseRoleNames.Admin, StringComparison.OrdinalIgnoreCase)))
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
            throw new ArgumentException("El nombre del curso es requerido.");
        }

        return await _academicRepository.CreateCourseAsync(siteId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseDetail> JoinCourseAsync(int siteId, CurrentUser currentUser, AcademicJoinCourseRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(request.InviteCode))
        {
            throw new ArgumentException("El código de invitación es requerido.");
        }

        return await _academicRepository.JoinCourseAsync(siteId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseDetail> GetCourseAsync(int siteId, long courseId, CurrentUser currentUser)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        if (currentUser.Role == UserRolesEnum.Administrador)
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
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "view course members");
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
            throw new ArgumentException("El nombre de usuario del miembro es requerido.");
        }

        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "add course members");
        request.UserId = request.UserId.Trim();
        request.Role = string.Equals(request.Role?.Trim(), CourseRoleNames.Assistant, StringComparison.OrdinalIgnoreCase)
            ? CourseRoleNames.Assistant
            : CourseRoleNames.Student;

        return await _academicRepository.AddCourseMemberAsync(siteId, courseId, request);
    }

    public async Task RemoveCourseMemberAsync(int siteId, long courseId, string memberUserId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException("El nombre de usuario del miembro es requerido.");
        }

        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "remove course members");
        await _academicRepository.RemoveCourseMemberAsync(siteId, courseId, memberUserId.Trim());
    }

    public async Task<AcademicCourseAssignment> CreateCourseAssignmentAsync(
        int siteId,
        long courseId,
        CurrentUser currentUser,
        AcademicCourseAssignmentCreationRequest request)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "create assignments");
        EnsureValidAssignmentRequest(request);
        return await _academicRepository.CreateCourseAssignmentAsync(siteId, courseId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseContentItem> CreateCourseMaterialAsync(
        int siteId,
        long courseId,
        CurrentUser currentUser,
        AcademicCourseMaterialCreationRequest request)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "create course materials");
        EnsureValidMaterialRequest(request);
        return await _academicRepository.CreateCourseMaterialAsync(courseId, currentUser.UserId, request);
    }

    private static void EnsureValidMaterialRequest(AcademicCourseMaterialCreationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("El título del material es requerido.");
        if (string.IsNullOrWhiteSpace(request.ContentBody) && string.IsNullOrWhiteSpace(request.ContentUrl))
            throw new ArgumentException("Agrega contenido del material o un enlace de recurso.");
        if (!string.IsNullOrWhiteSpace(request.ContentUrl)
            && (!Uri.TryCreate(request.ContentUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            throw new ArgumentException("La URL del material debe ser una dirección HTTP o HTTPS válida.");
    }

    public async Task<AcademicCourseContentItem> UpdateCourseMaterialAsync(
        int siteId,
        long courseId,
        long materialId,
        CurrentUser currentUser,
        AcademicCourseMaterialCreationRequest request)
    {
        if (materialId <= 0) throw new ArgumentException("El id del material es requerido.");
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "update course materials");
        EnsureValidMaterialRequest(request);
        return await _academicRepository.UpdateCourseMaterialAsync(courseId, materialId, request);
    }

    public async Task DeleteCourseMaterialAsync(int siteId, long courseId, long materialId, CurrentUser currentUser)
    {
        if (materialId <= 0) throw new ArgumentException("El id del material es requerido.");
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "delete course materials");
        await _academicRepository.DeleteCourseMaterialAsync(courseId, materialId);
    }

    public async Task ReorderCourseContentAsync(int siteId, long courseId, CurrentUser currentUser, IReadOnlyList<long> itemIds)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "reorder course content");

        var existingIds = course.Content.Select(item => item.ItemId).ToHashSet();
        var submittedIds = (itemIds ?? Array.Empty<long>()).ToList();
        if (submittedIds.Count == 0
            || submittedIds.Count != existingIds.Count
            || submittedIds.Distinct().Count() != submittedIds.Count
            || submittedIds.Any(id => !existingIds.Contains(id)))
        {
            throw new ArgumentException("El orden proporcionado no coincide con el contenido del curso.");
        }

        await _academicRepository.ReorderCourseContentAsync(courseId, submittedIds);
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
            throw new ArgumentException("El id de la tarea es requerido.");
        }

        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        EnsureCourseManager(course, currentUser, "update assignments");
        EnsureValidAssignmentRequest(request);
        return await _academicRepository.UpdateCourseAssignmentAsync(siteId, courseId, assignmentId, currentUser.UserId, request);
    }

    public async Task<AcademicCourseAssignmentDetailResponse> GetCourseAssignmentAsync(int siteId, long courseId, long assignmentId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (assignmentId <= 0)
        {
            throw new ArgumentException("El id de la tarea es requerido.");
        }

        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        if (IsAcademicManager(currentUser) && string.IsNullOrWhiteSpace(course.MemberRole))
        {
            course.MemberRole = GetAcademicManagerCourseRole(currentUser);
        }

        var assignment = await _academicRepository.GetCourseAssignmentAsync(siteId, courseId, assignmentId, currentUser.UserId);
        assignment.MemberRole = course.MemberRole;
        assignment.CanManage = course.CanManage;
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
            throw new ArgumentException("El id de la tarea es requerido.");
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
        report.CanDownloadCsv = course.CanManage;
        return report;
    }

    public async Task<AcademicStudentProgress> GetStudentProgressAsync(int siteId, long courseId, string studentUserId, CurrentUser currentUser)
    {
        var course = await GetCourseForCurrentUserAsync(siteId, courseId, currentUser);
        if (!course.CanManage
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
            throw new ArgumentException("La clave de la ruta de aprendizaje es requerida.");
        }

        return await _academicRepository.GetLearningPathAsync(siteId, learningPathKey);
    }

    public Task<LearningPathResponse> CreateLearningPathAsync(int siteId, CurrentUser currentUser, LearningPathAdminUpsertRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateLearningPathRequest(request);
        return _academicRepository.CreateLearningPathAsync(siteId, request);
    }

    public Task<LearningPathResponse> UpdateLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser, LearningPathAdminUpsertRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidateLearningPathRequest(request);
        return _academicRepository.UpdateLearningPathAsync(siteId, learningPathKey.Trim(), request);
    }

    public Task DeleteLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        return _academicRepository.DeleteLearningPathAsync(siteId, learningPathKey.Trim());
    }

    public Task<LearningPathResponse> CreateLearningPathStageAsync(int siteId, string learningPathKey, CurrentUser currentUser, LearningPathStageAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidateKey(request.Key, "Stage key");
        ValidateKey(request.Name, "Stage name");
        ValidatePositiveOrder(request.Order, "Stage order");
        return _academicRepository.CreateLearningPathStageAsync(siteId, learningPathKey.Trim(), request);
    }

    public Task<LearningPathResponse> LinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidatePositiveId(stageId, "Stage id");
        return _academicRepository.LinkLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId);
    }

    public Task UnlinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidatePositiveId(stageId, "Stage id");
        return _academicRepository.UnlinkLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId);
    }

    public Task<LearningPathResponse> UpdateLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser, LearningPathStageAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidateKey(request.Key, "Stage key");
        ValidateKey(request.Name, "Stage name");
        ValidatePositiveOrder(request.Order, "Stage order");
        return _academicRepository.UpdateLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId, request);
    }

    public Task DeleteLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        return _academicRepository.DeleteLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId);
    }

    public Task<LearningPathResponse> CreateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser, LearningPathTopicAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidateTopicRequest(request);
        return _academicRepository.CreateLearningPathTopicAsync(siteId, learningPathKey.Trim(), stageId, request);
    }

    public Task<LearningPathResponse> UpdateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, CurrentUser currentUser, LearningPathTopicAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidatePositiveId(topicId, "Topic id");
        ValidateTopicRequest(request);
        return _academicRepository.UpdateLearningPathTopicAsync(siteId, learningPathKey.Trim(), stageId, topicId, request);
    }

    public Task DeleteLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidatePositiveId(topicId, "Topic id");
        return _academicRepository.DeleteLearningPathTopicAsync(siteId, learningPathKey.Trim(), stageId, topicId);
    }

    public async Task<LearningPathProgressResponse> GetLearningPathProgressAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("La clave de la ruta de aprendizaje es requerida.");
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
            throw new ArgumentException("La clave de la ruta de aprendizaje es requerida.");
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
            throw new ArgumentException("ProblemId es requerido.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            throw new ArgumentException("SourceCode es requerido.");
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
        return await _academicRepository.GetCourseAsync(
            siteId,
            courseId,
            currentUser.UserId,
            currentUser.Role == UserRolesEnum.Administrador);
    }

    private static void EnsureCourseManager(AcademicCourseDetail course, CurrentUser currentUser, string action)
    {
        if (course.CanManage)
        {
            return;
        }

        throw new UnauthorizedAccessException($"Only the course's teachers, assistants or site administrators can {action}.");
    }

    private static void ValidateLearningPathManager(int siteId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);
        if (currentUser.Role != UserRolesEnum.Administrador)
        {
            throw new UnauthorizedAccessException("Only administrators can configure learning paths.");
        }
    }

    private static void ValidateLearningPathRequest(LearningPathAdminUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(request.Key, "Learning path key");
        ValidateKey(request.Title, "Learning path title");
        if (request.Version <= 0) throw new ArgumentException("Version debe ser positiva.");
    }

    private static void ValidateTopicRequest(LearningPathTopicAdminRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(request.Key, "Topic key");
        ValidateKey(request.Title, "Topic title");
        if (request.ProblemIds.Any(id => id <= 0)) throw new ArgumentException("Los ids de problema deben ser positivos.");
    }

    private static void ValidateKey(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} es requerido.");
    }

    private static void ValidatePositiveId(long id, string name)
    {
        if (id <= 0) throw new ArgumentException($"{name} es requerido.");
    }

    private static void ValidatePositiveOrder(int order, string name)
    {
        if (order <= 0) throw new ArgumentException($"{name} debe ser positivo.");
    }

    private static void EnsureValidAssignmentRequest(AcademicCourseAssignmentCreationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("El título de la tarea es requerido.");
        }

        var problemIds = request.ProblemIds
            .Where(problemId => problemId > 0)
            .Distinct()
            .ToList();

        if (problemIds.Count == 0)
        {
            throw new ArgumentException("Se requiere al menos un problema.");
        }

        request.ProblemIds = problemIds;
        request.OpensAt ??= DateTime.Now;
        request.DueAt ??= request.OpensAt.Value.AddDays(7);

        if (request.DueAt.Value < request.OpensAt.Value)
        {
            throw new ArgumentException("La fecha límite debe ser posterior a la fecha de apertura.");
        }

        if (request.LateDueAt.HasValue && request.LateDueAt.Value < request.DueAt.Value)
        {
            throw new ArgumentException("La fecha límite tardía debe ser posterior a la fecha límite.");
        }
    }

    private static string GetAcademicManagerCourseRole(CurrentUser currentUser)
    {
        return currentUser.Role switch
        {
            UserRolesEnum.Administrador => CourseRoleNames.Admin,
            UserRolesEnum.Auxiliar => CourseRoleNames.Assistant,
            UserRolesEnum.Docente => CourseRoleNames.Teacher,
            _ => string.Empty
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
