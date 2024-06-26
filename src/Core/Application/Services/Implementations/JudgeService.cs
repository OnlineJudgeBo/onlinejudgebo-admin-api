using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class JudgeService : IJudgeService
{
    private readonly IJudgeRepository _judgeRepository;
    private readonly ISolutionClientRepository _solutionClientRepository;
    private readonly ISolutionService _solutionService;
    private readonly IProblemService _problemService;
    private readonly IUserRepository _userRepository;
    private readonly IValidator<Problem> _userValidation;

    public JudgeService(
        IJudgeRepository judgeRepository,
        ISolutionClientRepository solutionClientRepository,
        IUserRepository userRepository,
        IProblemService problemService,
        ISolutionService solutionService,
        IValidator<Problem> ProblemValidation)
    {
        _judgeRepository = judgeRepository ?? throw new ArgumentNullException(nameof(judgeRepository));
        _solutionClientRepository = solutionClientRepository ?? throw new ArgumentNullException(nameof(solutionClientRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _solutionService = solutionService ?? throw new ArgumentNullException(nameof(solutionService));
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

    public async Task RemoteExecutionAsync(RemoteExecutionRequest request, string userId)
    {

        Problem problem = await _problemService.GetProblemByIdAsync(request.JudgeProblemId);
        if (problem == null)
        {
            throw new Exception($"The problem with ID {request.JudgeProblemId} is not a valid id.");
        }

        User user = await _userRepository.GetUserById(userId);
        if (user == null)
        {
            throw new Exception($"The user is not valid");
        }

        int solution_id = await _solutionService.SaveSolutionRemoteAsync(userId, request.JudgeProblemId, request.JudgeLanguageId, request.ClientSource, request.ClientSubmitId);

        if (solution_id == 0)
        {
            throw new Exception("Error in process the remote execution.");
        }

        await _solutionClientRepository.SaveSourceCodeAsync(solution_id, request.ClientSource);
        await _solutionClientRepository.SaveRemoteSolutionAsync(solution_id, request.ClientId);
    }
}
