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
}

