using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class JudgeService : IJudgeService
{
    private readonly IJudgeRepository _judgeRepository;

    private readonly IValidator<Problem> _userValidation;

    public JudgeService(
        IJudgeRepository judgeRepository,
        IValidator<Problem> ProblemValidation)
    {
        _judgeRepository = judgeRepository ?? throw new ArgumentNullException(nameof(judgeRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task RejudgeSolutionByIdAsync(int solutionId)
    {
        await _judgeRepository.RejudgeSolutionByIdAsync(solutionId);
    }

    public async Task RejudgeSolutionByProblemIdAsync(int problemId)
    {
        await _judgeRepository.RejudgeSolutionByProblemIdAsync(problemId);
    }
}
