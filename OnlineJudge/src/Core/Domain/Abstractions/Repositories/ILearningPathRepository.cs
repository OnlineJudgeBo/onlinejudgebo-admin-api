using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ILearningPathRepository
{
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
}
