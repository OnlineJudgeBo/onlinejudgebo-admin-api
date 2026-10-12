using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface ILearningPathService
{
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
}
