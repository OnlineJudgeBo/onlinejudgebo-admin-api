using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IAcademicService
{
    Task<IEnumerable<AcademicInstitution>> GetInstitutionsAsync(int siteId, CurrentUser currentUser);

    Task<IEnumerable<AcademicInstitutionRankingItem>> GetInstitutionsRankingAsync(int siteId, CurrentUser currentUser, int limit);

    Task<IEnumerable<AcademicCourseSummary>> GetMyCoursesAsync(int siteId, CurrentUser currentUser);

    Task<IEnumerable<AcademicCourseSummary>> GetManageableCoursesAsync(int siteId, CurrentUser currentUser);

    Task<AcademicCourseDetail> CreateCourseAsync(int siteId, CurrentUser currentUser, AcademicCourseCreationRequest request);

    Task<AcademicCourseDetail> JoinCourseAsync(int siteId, CurrentUser currentUser, AcademicJoinCourseRequest request);

    Task<AcademicCourseDetail> GetCourseAsync(int siteId, long courseId, CurrentUser currentUser);

    Task<IEnumerable<AcademicCourseMember>> GetCourseMembersAsync(int siteId, long courseId, CurrentUser currentUser);

    Task<AcademicCourseMember> AddCourseMemberAsync(int siteId, long courseId, CurrentUser currentUser, AcademicCourseMemberCreationRequest request);

    Task RemoveCourseMemberAsync(int siteId, long courseId, string memberUserId, CurrentUser currentUser);

    Task<AcademicCourseAssignment> CreateCourseAssignmentAsync(int siteId, long courseId, CurrentUser currentUser, AcademicCourseAssignmentCreationRequest request);

    Task<AcademicCourseContentItem> CreateCourseMaterialAsync(int siteId, long courseId, CurrentUser currentUser, AcademicCourseMaterialCreationRequest request);

    Task<AcademicCourseContentItem> UpdateCourseMaterialAsync(int siteId, long courseId, long materialId, CurrentUser currentUser, AcademicCourseMaterialCreationRequest request);

    Task DeleteCourseMaterialAsync(int siteId, long courseId, long materialId, CurrentUser currentUser);

    Task ReorderCourseContentAsync(int siteId, long courseId, CurrentUser currentUser, IReadOnlyList<long> itemIds);

    Task<AcademicCourseAssignment> UpdateCourseAssignmentAsync(int siteId, long courseId, long assignmentId, CurrentUser currentUser, AcademicCourseAssignmentCreationRequest request);

    Task<AcademicCourseAssignmentDetailResponse> GetCourseAssignmentAsync(int siteId, long courseId, long assignmentId, CurrentUser currentUser);

    Task<PublicSubmissionsResponse> GetCourseAssignmentSubmissionsAsync(int siteId, long courseId, long assignmentId, int page, int pageSize, CurrentUser currentUser);

    Task<IEnumerable<AcademicCourseRankingItem>> GetCourseRankingAsync(int siteId, long courseId, CurrentUser currentUser);

    Task<AcademicCourseReportResponse> GetCourseReportAsync(int siteId, long courseId, CurrentUser currentUser);

    Task<AcademicStudentProgress> GetStudentProgressAsync(int siteId, long courseId, string studentUserId, CurrentUser currentUser);

    Task<IEnumerable<LearningPathTrackSummary>> GetLearningPathsAsync(int siteId, CurrentUser currentUser);

    Task<LearningPathResponse> GetLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser);

    Task<LearningPathResponse> CreateLearningPathAsync(int siteId, CurrentUser currentUser, LearningPathAdminUpsertRequest request);
    Task<LearningPathResponse> UpdateLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser, LearningPathAdminUpsertRequest request);
    Task DeleteLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser);
    Task<LearningPathResponse> CreateLearningPathStageAsync(int siteId, string learningPathKey, CurrentUser currentUser, LearningPathStageAdminRequest request);
    Task<LearningPathResponse> LinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser);
    Task UnlinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser);
    Task<LearningPathResponse> UpdateLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser, LearningPathStageAdminRequest request);
    Task DeleteLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser);
    Task<LearningPathResponse> CreateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser, LearningPathTopicAdminRequest request);
    Task<LearningPathResponse> UpdateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, CurrentUser currentUser, LearningPathTopicAdminRequest request);
    Task DeleteLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, CurrentUser currentUser);

    Task<LearningPathProgressResponse> GetLearningPathProgressAsync(int siteId, string learningPathKey, CurrentUser currentUser);

    Task<LearningPathProgressResponse> SaveLearningPathProgressAsync(
        int siteId,
        string learningPathKey,
        CurrentUser currentUser,
        LearningPathProgressUpdateRequest request);

    Task<AcademicSubmissionResponse> SubmitAsync(CurrentUser currentUser, AcademicSubmissionRequest request);
}
