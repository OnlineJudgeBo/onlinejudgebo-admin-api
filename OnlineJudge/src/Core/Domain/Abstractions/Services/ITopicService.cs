using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface ITopicService
{
    Task<IEnumerable<Topic>> GetAllTopicsAsync();
    Task AddTopicAsync(Topic newTopic);
    Task AddClassificationToTopic(int id, Topic newTopic);
    Task UpdateClassification(Classification classification, int id);
}

