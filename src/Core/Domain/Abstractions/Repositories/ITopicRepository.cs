using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ITopicRepository
{
    Task<IEnumerable<Topic>> GetAllTopicsAsync();

    Task AddClassificationsToProblemAsync(int problem_id, IEnumerable<Classification> topics);

    Task RemoveAllClassificationsFromProblemAsync(int problemId);

    Task CreateTopic(Topic topic);
    Task AddClassificationToTopic(Topic topic);
    Task UpdateClassification(Classification classification);
    Task<Classification> GetClassificationById(int classificationId);
}
