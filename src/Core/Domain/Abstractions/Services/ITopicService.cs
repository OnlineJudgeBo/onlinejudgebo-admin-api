using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface ITopicService
{
    Task<IEnumerable<Topic>> GetAllTopicsAsync();
    Task AddTopicAsync(Topic newTopic);
    Task AddClassificationToTopic(Topic newTopic);
    Task UpdateClassification(Classification classification, int id);
}

