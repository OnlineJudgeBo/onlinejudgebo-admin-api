using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IAcademicRepository
{
    Task<IEnumerable<AcademicInstitution>> GetInstitutionsAsync(int siteId);

    Task<IEnumerable<AcademicInstitutionRankingItem>> GetInstitutionsRankingAsync(int siteId, int limit);

    Task<IEnumerable<AcademicCourseSummary>> GetMyCoursesAsync(int siteId, string userId);

    Task<IEnumerable<AcademicCourseSummary>> GetManageableCoursesAsync(int siteId, string userId, bool includeAllCourses);

    Task<AcademicCourseDetail> CreateCourseAsync(int siteId, string userId, AcademicCourseCreationRequest request);

    Task<AcademicCourseDetail> JoinCourseAsync(int siteId, string userId, AcademicJoinCourseRequest request);

    Task<AcademicCourseDetail> GetCourseAsync(int siteId, long courseId, string userId, bool allowAdminAccess);

    Task<IEnumerable<AcademicCourseMember>> GetCourseMembersAsync(int siteId, long courseId);

    Task<AcademicCourseMember> AddCourseMemberAsync(int siteId, long courseId, AcademicCourseMemberCreationRequest request);

    Task RemoveCourseMemberAsync(int siteId, long courseId, string userId);

    Task<AcademicCourseAssignment> CreateCourseAssignmentAsync(int siteId, long courseId, string userId, AcademicCourseAssignmentCreationRequest request);

    Task<AcademicCourseContentItem> CreateCourseMaterialAsync(long courseId, string userId, AcademicCourseMaterialCreationRequest request);

    Task<AcademicCourseContentItem> UpdateCourseMaterialAsync(long courseId, long materialId, AcademicCourseMaterialCreationRequest request);

    Task DeleteCourseMaterialAsync(long courseId, long materialId);

    Task ReorderCourseContentAsync(long courseId, IReadOnlyList<long> itemIds);

    Task<AcademicCourseAssignment> UpdateCourseAssignmentAsync(int siteId, long courseId, long assignmentId, string userId, AcademicCourseAssignmentCreationRequest request);

    Task<AcademicCourseAssignmentDetailResponse> GetCourseAssignmentAsync(int siteId, long courseId, long assignmentId, string currentUserId);

    Task<PublicSubmissionsResponse> GetCourseAssignmentSubmissionsAsync(int siteId, long courseId, long assignmentId, int page, int pageSize);

    Task<bool> CanViewCourseSubmissionSourceAsync(int siteId, int solutionId, string userId, bool includeAdminAccess);

    Task<IEnumerable<AcademicCourseRankingItem>> GetCourseRankingAsync(int siteId, long courseId);

    Task<AcademicCourseReportResponse> GetCourseReportAsync(int siteId, long courseId);

    Task<AcademicStudentProgress> GetStudentProgressAsync(int siteId, long courseId, string userId);

    Task<IEnumerable<LearningPathTrackSummary>> GetLearningPathsAsync(int siteId);

    Task<LearningPathResponse> GetLearningPathAsync(int siteId, string learningPathKey);

    Task<LearningPathResponse> CreateLearningPathAsync(int siteId, LearningPathAdminUpsertRequest request);
    Task<LearningPathResponse> UpdateLearningPathAsync(int siteId, string learningPathKey, LearningPathAdminUpsertRequest request);
    Task DeleteLearningPathAsync(int siteId, string learningPathKey);
    Task<LearningPathResponse> CreateLearningPathStageAsync(int siteId, string learningPathKey, LearningPathStageAdminRequest request);
    Task<LearningPathResponse> LinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId);
    Task UnlinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId);
    Task<LearningPathResponse> UpdateLearningPathStageAsync(int siteId, string learningPathKey, long stageId, LearningPathStageAdminRequest request);
    Task DeleteLearningPathStageAsync(int siteId, string learningPathKey, long stageId);
    Task<LearningPathResponse> CreateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, LearningPathTopicAdminRequest request);
    Task<LearningPathResponse> UpdateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, LearningPathTopicAdminRequest request);
    Task DeleteLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId);

    Task<LearningPathProgressResponse> GetLearningPathProgressAsync(int siteId, string learningPathKey, string userId);

    Task<LearningPathProgressResponse> SaveLearningPathProgressAsync(int siteId, string learningPathKey, string userId, LearningPathProgressUpdateRequest request);

    Task<AcademicSubmissionResponse> SubmitAsync(int siteId, string userId, AcademicSubmissionRequest request);
}
