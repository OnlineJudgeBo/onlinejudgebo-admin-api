using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ContestService : IContestService
{
    private readonly IContestsRepository _contestRepository;

    private readonly IValidator<Problem> _userValidation;

    public ContestService(
        IContestsRepository topicRepository,
        IValidator<Problem> ProblemValidation)
    {
        _contestRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Contest>> GetAllContestAsync()
    {
        return await _contestRepository.GetAllContestsAsync();
    }
}

