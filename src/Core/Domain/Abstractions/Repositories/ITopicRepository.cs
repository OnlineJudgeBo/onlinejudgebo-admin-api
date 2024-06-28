using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ITopicRepository
{
    Task<IEnumerable<Topic>> GetAllTopicsAsync();

    Task AddClassificationsToProblemAsync(int problem_id, IEnumerable<Classification> topics);

    Task RemoveAllClassificationsFromProblemAsync(int problemId);

    Task CreateTopic(Topic topic);
    Task AddClassificationToTopic(Topic topic);
    Task<Classification> GetClassificationById(int classificationId);
    Task UpdateClassification(Classification classification, int id);
}
