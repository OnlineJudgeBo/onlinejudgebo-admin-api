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

    public async Task<RejudgeOperationResponse> RejudgeSolutionByIdAsync(int siteId, int solutionId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(solutionId, "SolutionId");
        var matched = await _judgeRepository.RejudgeSolutionByIdAsync(siteId, solutionId);
        return BuildRejudgeResponse(siteId, "solution", matched);
    }

    public async Task<RejudgeOperationResponse> RejudgeSolutionByProblemIdAsync(int siteId, int problemId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(problemId, "ProblemId");
        var matched = await _judgeRepository.RejudgeSolutionByProblemIdAsync(siteId, problemId);
        return BuildRejudgeResponse(siteId, "problem", matched);
    }

    public async Task<RejudgeOperationResponse> RejudgeSolutionByContestIdAsync(int siteId, int contestId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(contestId, "ContestId");
        var matched = await _judgeRepository.RejudgeSolutionByContestIdAsync(siteId, contestId);
        return BuildRejudgeResponse(siteId, "contest", matched);
    }

    public async Task<RejudgeOperationResponse> RejudgeSolutionsByRangeAsync(int siteId, int fromSolutionId, int toSolutionId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(fromSolutionId, "FromSolutionId");
        ValidatePositiveId(toSolutionId, "ToSolutionId");

        if (toSolutionId < fromSolutionId)
        {
            throw new ArgumentException("El rango de soluciones es inválido.");
        }

        var matched = await _judgeRepository.RejudgeSolutionsByRangeAsync(siteId, fromSolutionId, toSolutionId);
        return BuildRejudgeResponse(siteId, "range", matched);
    }

    public async Task<RejudgeOperationResponse> RejudgeSolutionsByLanguageAsync(int siteId, int languageId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(languageId, "LanguageId");
        var matched = await _judgeRepository.RejudgeSolutionsByLanguageAsync(siteId, languageId);
        return BuildRejudgeResponse(siteId, "language", matched);
    }

    public Task<RejudgeHistoryResponse> GetRejudgeHistoryAsync(int siteId, int limit)
    {
        ValidateSite(siteId);
        return _judgeRepository.GetRejudgeHistoryAsync(siteId, Math.Min(200, Math.Max(1, limit)));
    }

    public async Task RemoteExecutionAsync(RemoteExecutionRequest request, string userId, int siteId)
    {

        Problem problem = await _problemService.GetProblemByIdAsync(request.JudgeProblemId);
        if (problem == null)
        {
            throw new Exception($"The problem with ID {request.JudgeProblemId} is not a valid id.");
        }

        User user = await _userRepository.GetUserById(userId, siteId);
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

    private static RejudgeOperationResponse BuildRejudgeResponse(int siteId, string scope, int matched)
    {
        return new RejudgeOperationResponse
        {
            SiteId = siteId,
            Scope = scope,
            Matched = matched,
            RequestedAtUtc = DateTime.Now
        };
    }

    private static void ValidateSite(int siteId)
    {
        if (siteId <= 0)
        {
            throw new ArgumentException("SiteId inválido.");
        }
    }

    private static void ValidatePositiveId(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{name} inválido.");
        }
    }
}
