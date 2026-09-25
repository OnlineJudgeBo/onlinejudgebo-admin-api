using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class TopicService : ITopicService
{
    private readonly ITopicRepository _topicRepository;

    private readonly IValidator<Problem> _userValidation;

    public TopicService(
        ITopicRepository topicRepository,
        IValidator<Problem> ProblemValidation)
    {
        _topicRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Topic>> GetAllTopicsAsync()
    {
        return await _topicRepository.GetAllTopicsAsync();
    }

    public async Task AddTopicAsync(Topic newTopic)
    {
        await _topicRepository.CreateTopic(newTopic);
    }

    public async Task AddClassificationToTopic(int id, Topic topic)
    {
        if (topic.Classifications == null || topic.Classifications.Count == 0)
        {
            throw new ArgumentException("No se proporcionaron clasificaciones.");
        }

        await _topicRepository.AddClassificationToTopic(id, topic);
    }

    public async Task UpdateClassification(Classification classification, int classificationId)
    {
        var existingClassification = await _topicRepository.GetClassificationById(classificationId);

        if (existingClassification == null)
        {
            throw new ArgumentException("Clasificación no encontrada.");
        }

        await _topicRepository.UpdateClassification(classification, classificationId);
    }
}

